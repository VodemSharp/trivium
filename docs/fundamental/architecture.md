---
title: Architecture
last_reviewed: 2026-11-21
code_globs:
  - src/Trivium/**
  - src/Trivium.Generators/**
  - src/Trivium.Hosting/**
  - src/Trivium.AspNetCore/**
---

# Architecture

[← Back to AGENTS.md](../../AGENTS.md)

**Related:** [Docs index](../README.md) · [Roadmap](../plan/roadmap.md) ·
[Deduplication](../reference/deduplication.md) · [Quality gates](quality.md) ·
[Telegram coverage](../reference/telegram-coverage.md) · [Diagnostics](../reference/diagnostics.md)

## Goals

1. Turn one Telegram update into exactly one handler invocation with deterministic semantics.
2. Make handler registration compile-time checked and reflection-free.
3. Support polling and webhooks equally, including many bots in one process.
4. Keep persistence out of the framework: the core stores nothing, and schema, stores, migrations and conversation state
   belong to the application. Authorization belongs to the application too: the core ships guards, not roles or
   permissions.
5. Be AOT/trimming-safe, observable and testable from the first commit.

## Non-goals

- Wrapping every Bot API method. `Telegram.Bot` already does that.
- A payment, gift, story or business-workflow framework.
- An ORM, admin UI or multi-tenant SaaS control plane.
- Runtime handler discovery or plugin loading.

## Package graph

```text
Trivium            core: contexts, routes, dispatch, guards, ingress filters,
                   IngressOutcome                            (Telegram.Bot only)
├── Trivium.Hosting          DI, polling, multi-bot, health, filter registration
│   ├── Trivium.Deduplication    dedup filter, in-memory default, UseDeduplication()
│   └── Trivium.AspNetCore       webhook endpoint, secret, limits
├── Trivium.OpenTelemetry    ActivitySource, Meter, instrumentation helpers
└── Trivium.Testing          fake client, update builders, test host

Trivium.Generators  compile-time only; referenced by user projects
Your application    owns schema, stores, migrations and conversation state
```

Dependency rules:

- `Trivium` references only `Telegram.Bot`; not hosting, ASP.NET Core, telemetry, any database driver or dedup policy.
- `Trivium.Deduplication` may reference `Trivium.Hosting` and `Microsoft.Extensions.*`; no server client, no database
  driver.
- `Trivium.Hosting` may reference `Trivium` and `Microsoft.Extensions.*`.
- `Trivium.AspNetCore` may reference `Trivium.Hosting` and ASP.NET Core; never `Trivium.Deduplication`.
- `Trivium.Generators` references Roslyn only; it must not reference `Microsoft.CodeAnalysis.Workspaces`.
- No project under `src` references Npgsql, Dapper, a database provider or a migration tool.
  Persistence is application code, and the samples show how to write it.
- These rules are enforced by architecture tests.

Ingress filters are core — the dispatcher runs them before routing — but a filter implementation is policy. Dedup ships
in `Trivium.Deduplication`; `Trivium` only knows `IUpdateIngressFilter` and the outcome a filter returns. A shared
backend is application code implementing `IUpdateDeduplicator`. The key layout, lease and window rules are in
[Deduplication](../reference/deduplication.md).

## Runtime pipeline

```text
 ┌──────────────┐     ┌──────────────┐     ┌─────────────────┐
 │ Polling loop │     │  Webhook     │     │  In-process     │
 │ (Background) │     │  endpoint    │     │  test dispatch  │
 └──────┬───────┘     └──────┬───────┘     └────────┬────────┘
        └────────────────────┼──────────────────────┘
                             ▼
                  ┌─────────────────────┐
                  │  Update ingress     │  validate bot, size, secret,
                  │  (IUpdateIngress)   │  run ingress filters, build envelope
                  └──────────┬──────────┘
                             ▼
                  ┌─────────────────────┐
                  │  IUpdateDispatcher  │  create scope, resolve actor
                  └──────────┬──────────┘
                             ▼
                  ┌─────────────────────┐
                  │    Route matcher    │  kind → exact → prefix → default
                  └──────────┬──────────┘
                             ▼
                  ┌─────────────────────┐
                  │   Guard pipeline    │  global → group → route
                  │  IUpdateGuard       │  deny = stop, never fallback
                  └──────────┬──────────┘
                             ▼
                  ┌─────────────────────┐
                  │    Middleware       │  before hooks (ordered)
                  │  IUpdateMiddleware  │  may short-circuit
                  └──────────┬──────────┘
                             ▼
                  ┌─────────────────────┐
                  │   Generated invoker │  direct call, no reflection
                  └──────────┬──────────┘
                             ▼
                  ┌─────────────────────┐
                  │    Middleware       │  after hooks (reverse order)
                  └──────────┬──────────┘
                             ▼
                  ┌─────────────────────┐
                  │  IUpdateErrorHandler│  log, classify, surface
                  └─────────────────────┘
```

Properties:

- **One scope per update.** Scoped services (application stores, user context) live and die with the update.
- **Dedup is transport-agnostic.** Polling, webhooks and in-process dispatch share `IUpdateIngressFilter`; the filter
  ships in `Trivium.Deduplication` and is enabled with `UseDeduplication()`, so it then runs on every transport. The
  polling offset remains the primary acknowledgement.
- **One route per update.** Fan-out is not supported; compose multiple concerns inside the handler or through
  middleware.
- **No mutable global state.** The route table, registry and options are immutable after startup.
- **Cancellation everywhere.** Host shutdown and per-handler timeouts flow into the token. The webhook's request abort
  token does not: a transport timeout must not cancel work whose reply is sent through `ITelegramBotClient`.
- **Errors never escape the loop.** They are classified (`ApiRequestException`, `OperationCanceledException`,
  handler exception, data access exception) and handled by the configured error handler.

## Core types (indicative)

```csharp
public readonly record struct BotId(long Value);
public readonly record struct UserId(long Value);
public readonly record struct ChatId(long Value);
public readonly record struct MessageId(long Value);
public readonly record struct UpdateId(long Value);

public enum UpdateKind
{
    Unknown = 0,
    Message,
    EditedMessage,
    ChannelPost,
    EditedChannelPost,
    CallbackQuery,
    InlineQuery,
    ChosenInlineResult,
    ShippingQuery,
    PreCheckoutQuery,
    Poll,
    PollAnswer,
    ChatMember,
    MyChatMember,
    ChatJoinRequest,
    BusinessConnection,
    BusinessMessage,
    EditedBusinessMessage,
    DeletedBusinessMessages,
    MessageReaction,
    MessageReactionCount,
    ChatBoost,
    RemovedChatBoost,
    PurchasedPaidMedia,
    GuestMessage,
    ManagedBot,
    Subscription,
    StoppedMessageGeneration
}

public sealed class TriviumContext
{
    public required BotId BotId { get; init; }
    public required ITelegramBotClient Bot { get; init; }
    public required Update Update { get; init; }
    public required UpdateKind Kind { get; init; }
    public required IServiceProvider Services { get; init; }
    public required CancellationToken CancellationToken { get; init; }
    public Actor? Actor { get; init; }
    public Chat? Chat { get; init; }
    public RouteInfo Route { get; init; } = RouteInfo.None;
    public IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

    public Message? Message => Update.Message;
    public CallbackQuery? CallbackQuery => Update.CallbackQuery;
    // ...one typed accessor per update kind, no null-forgiving in user code
}
```

`TriviumContext` is a sealed class, not a struct: it holds service references, a dictionary and is passed through
async boundaries. Value objects are used for identifiers so Telegram's many `long` values cannot be mixed accidentally.

## Handler model

### Attributes

```csharp
[MessageCommand("/start", Description = "Start the bot")]
public static ValueTask Start(TriviumContext ctx, CancellationToken ct)
{
    return ctx.ReplyAsync("Hello!", ct);
}

[CallbackQuery("menu:settings")]
public static ValueTask Settings(TriviumContext ctx, SettingsCallback data, CancellationToken ct)
{
    return ctx.EditAsync($"Theme: {data.Theme}", ct);
}

[CommandGroup("admin")]
public sealed class AdminCommands
{
    [MessageCommand("/grant")]
    [ModeratorOnly] // your guard: an attribute backed by IUpdateGuard
    public async ValueTask Grant(TriviumContext ctx, CommandArguments args, CancellationToken ct)
    {
        // ...
    }
}
```

`[ModeratorOnly]` is illustrative: Trivium ships no role model. The attribute is yours; it implements `IUpdateGuard`
and reads whatever principal your application already owns.

`[UpdateHandler]` without a kind is the catch-all; `[UpdateHandler(UpdateKind.X)]` targets one update kind.

Class-level attributes compose onto every handler in the class: your guard attributes become guards, `[Bot]` scopes the
routes, and `[CommandGroup]` contributes command-catalog metadata. Group middleware is registered
explicitly (`AddGroup("admin").UseMiddleware<T>()`); there is no mutable runtime group object.

Attributes are the only route-authoring model. There is no delegate-based registration call alongside them: one
authoring model means one signature validator, one diagnostic set, and no choice to explain. Host configuration stays
fluent — `AddBot`, `UsePolling` and `AddGroup(...).UseMiddleware<T>()` are builder calls, not route definitions.

### Route table contract

The generator emits a `TriviumRouteTable` into the consuming assembly, and the application registers it once:

```csharp
builder.Services.AddTrivium<TriviumRouteTable>(trivium =>
{
    trivium.AddBot("main", bot => bot.UsePolling());
});
```

The table and its route descriptors are public because generated code lives in the user's assembly. They are data: an
exact-match dictionary, a sorted prefix array, the callback dictionary, the defaults and the per-bot scopes. They are
the compilation target, not a second way to write handlers, and `Trivium.Testing` populates the same types so tests can
dispatch a small table without a generator. A missing or mismatched table fails at startup.

Dispatch is direct call sites: no `Delegate`, no `DynamicInvoke` and no runtime type inspection anywhere in routing or
dispatch.

### Supported signatures

| Parameter                                     | Allowed          | Notes                       |
| --------------------------------------------- | ---------------- | --------------------------- |
| `TriviumContext`                              | Yes, at most one | Optional                    |
| Typed payload (`Message`, `CallbackQuery`, …) | Yes, at most one | Must match the update kind  |
| `CancellationToken`                           | Yes, at most one | From transport and shutdown |
| `CommandArguments`                            | Yes, at most one | Parsed text tail            |
| `[CallbackData]` payload                      | Yes, at most one | Callback routes only        |
| Anything else                                 | No               | Compile-time diagnostic     |

A second parameter in the same slot is a diagnostic (`TRIVIUM0005`; `TRIVIUM0012` for a second `CancellationToken`).

The typed parameter is the update's primary payload; related objects come from the context, for example
`ctx.CallbackQuery.Message` for the message a callback was attached to.

Return types follow the same closed-list rule:

| Return                | Allowed                     |
| --------------------- | --------------------------- |
| `Task`                | Yes                         |
| `Task<T>`             | Yes (result ignored/logged) |
| `ValueTask`           | Yes                         |
| `ValueTask<T>`        | Yes                         |
| `void` / `async void` | No                          |

Services are injected through the handler class constructor (instance handlers) or resolved from
`ctx.Services` (static handlers).

## Source generator

### Inputs

- Methods/classes annotated with update attributes.
- `[CommandGroup]`, `[Bot]` and user-defined guard attributes.
- `[CallbackData]` partial types.

There is no partial-class hook for hand-written registrations: the attribute set is the complete input.

### Outputs

| Generated file                       | Contents                                                                     |
| ------------------------------------ | ---------------------------------------------------------------------------- |
| `TriviumRouteTable.g.cs`             | Immutable route table per bot, exact dictionaries, sorted prefixes, defaults |
| `TriviumHandlerInvoker.g.cs`         | Direct calls to static methods / resolved instance handlers                  |
| `TriviumServiceMap.g.cs`             | DI registrations with chosen lifetimes                                       |
| `TriviumCommandCatalog.g.cs`         | Command catalog for `SetMyCommands` (text, description, scope, language)     |
| `TriviumCallbackDataSerializer.g.cs` | Compact encode/decode + STJ source-gen context per payload                   |
| `TriviumAllowedUpdateSet.g.cs`       | Minimal `AllowedUpdates` set per bot                                         |
| `TriviumStartupValidator.g.cs`       | Startup checks that cannot be expressed as diagnostics                       |

File names and boundaries in this table are indicative; the exact generated surface is settled by tests in M2.
Generated type names are singular and describe the artifact they own (`TriviumRouteTable`, `TriviumServiceMap`,
`TriviumCommandCatalog`), never plural container classes (`TriviumRoutes`, `TriviumServices`, `TriviumCommands`).

### Diagnostics

Diagnostics are documented in [the diagnostics reference](../reference/diagnostics.md) and kept in step with the
generator as each rule lands. IDs and severities are provisional until the implementation exists and are reviewed
before M2 exits. A build with an ambiguous or duplicate route must fail; warnings are only for quality issues such as
a missing description or a constant callback payload that may exceed the size limit. House-style analyzers are
described in [in-house analyzers](quality.md#In-house%20analyzers).

### Why a generator

- Direct calls: no `MethodInfo.Invoke`, no `TargetInvocationException`, no sync-over-async.
- Build-time validation: duplicate/ambiguous routes cannot ship.
- AOT/trimming: no runtime discovery.
- Command catalog and `AllowedUpdates` are derived from the same source of truth as routing.
- IDE navigation: attributes are normal code; generated code is navigable and deterministic.

## Routing rules

These rules are the contract; the tests follow them.

| Step | Rule                                                                                                       |
| ---- | ---------------------------------------------------------------------------------------------------------- |
| 1    | Update kind must match the route's kind.                                                                   |
| 2    | Exact text/callback path wins over parameterized and default routes.                                       |
| 3    | Parameterized routes match only on a literal prefix followed by end-of-string or a separator (whitespace). |
| 4    | Among matching parameterized routes, the **longest** literal prefix wins. Ties are startup errors.         |
| 5    | The default route runs only when no exact/parameterized route matches.                                     |
| 6    | Multiple defaults for the same kind and bot are startup errors.                                            |
| 7    | `/start@MyBot` matches `/start` when the mention is this bot's username; other mentions do not match.      |
| 8    | Text comparison is ordinal and case-sensitive by default; `IgnoreCase` is per route.                       |
| 9    | Caption fallback for media messages is per route (`AllowCaption`); it is not global.                       |
| 10   | A failed guard stops the update. It never falls through to another route, default or unfiltered list.      |
| 11   | Exactly one route runs per update. Fan-out is not supported; compose concerns inside the handler.          |
| 12   | No match is logged at debug/info and optionally handed to a catch-all; it is never an exception.           |

Arguments:

```text
Route:   /start
Update:  /start@MyBot   hello world
Match:   exact (mention normalized)
Args:    "hello world"   (trimmed tail; separator removed)
```

`CommandArguments` exposes `Raw`, `Values` (quote-aware), `TryGet<T>` and `IsEmpty`. It never throws on malformed input;
`TryGet` returns `false` with a reason.

## Guards and middleware

### Guards

`IUpdateGuard` answers allow/deny for a route. Guards are resolved from the update scope and evaluated in a documented
order:

```csharp
public interface IUpdateGuard
{
    ValueTask<GuardResult> EvaluateAsync(TriviumContext ctx, CancellationToken ct);
}

public readonly record struct GuardResult(bool IsAllowed, string? Reason)
{
    public static GuardResult Allow { get; } = new(true, null);
    public static GuardResult Deny(string reason) => new(false, reason);
}
```

1. global guards (registered on `TriviumBuilder`);
2. group guards (`[CommandGroup]` / group builder);
3. route guards (attributes on the handler).

Guards are user code: custom `IUpdateGuard` implementations and guard attributes registered by the user. Trivium ships no
guard that reads a database; a state gate is an ordinary guard in your application.

A guard receives an `TriviumContext` and a read-only `RouteInfo`. It must not mutate the context. Denial returns
`GuardResult.Deny(reason)`; the dispatcher applies the configured denied behavior:

| Behavior           | Effect                                |
| ------------------ | ------------------------------------- |
| `Ignore` (default) | Stop silently, log at debug/info.     |
| `Reply`            | Send a configured message, then stop. |
| `Custom`           | Invoke an `IDeniedUpdateHandler`.     |

No denial behavior falls through to another route.

### Authorization is application-defined

Trivium evaluates guards; it ships no role or permission model. Read your own principal from whatever store you
already have, decide, and return `GuardResult.Deny(reason)`. Register a custom `IUpdateGuard` globally, per group or as
a route attribute. Trivium guarantees evaluation order, immunity from middleware ordering, and that a denial stops the
route.

### Why guards are not ordinary middleware

A guard is a declarative predicate attached to a route (or globally/group-scoped) that answers one question: may this
route run? The dispatcher evaluates the guards for the selected route before the middleware pipeline, so a denial has
one tested meaning and cannot "continue" by forgetting to call `next`.

Middleware is imperative and composable: it observes, mutates, wraps before/after behavior and may short-circuit.
Internally both share invocation plumbing, but guards are not registered as user middleware and do not participate in
middleware ordering. If a deny were ordinary middleware, every implementation could accidentally call `next` after a
denial, and the route table could no longer describe which routes are protected.

### Middleware

`IUpdateMiddleware` wraps the handler:

```csharp
public interface IUpdateMiddleware
{
    ValueTask InvokeAsync(TriviumContext ctx, UpdateDelegate next);
}
```

Typical uses: user tracking, timing/metrics, transaction boundaries, state snapshot loading. Middleware order is
explicit: global → group → route, and after-hooks run in reverse. Middleware may short-circuit by not calling `next`.

Before/after "actions" from the old library are middleware, not a separate concept. This removes the ordering ambiguity
and the group composition bugs.

## Multi-bot

```csharp
builder.Services.AddTrivium(trivium =>
{
    trivium.AddBot("support", bot => bot
        .UsePolling()
        .UseRoutes<SupportRoutes>());

    trivium.AddBot("shop", bot => bot
        .UseWebhook("/telegram/shop")
        .UseRoutes<ShopRoutes>());

    trivium.AddBotsFromConfiguration(configuration.GetSection("Trivium:Bots"));
});
```

Design:

- `IBotRegistry` is immutable after startup; `BotDescriptor` contains name, `BotId`, username, transport, route scope and
  options. The username is resolved via `GetMe` or explicit configuration and used for `/cmd@bot` normalization.
  Adding/removing bots requires a restart; there is no hot reload in 1.0.
- `ITelegramBotClient` is registered as a keyed singleton per bot; a scoped `BotContext` exposes the current bot.
- Routes are global by default; `[Bot("name")]` or `UseRoutes<T>()` narrows them. The startup route inventory logs the
  effective matrix.
- Application storage includes `bot_id`; every query is scoped to one bot and cannot accidentally read across bots.
- Transports are independent: one bot can poll while another uses a webhook.
- No ambient static current-bot state; `TriviumContext.BotId` is authoritative.

## Hosting

### Polling (`Trivium.Hosting`)

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddTrivium(a => a.AddBot("main", b => b.UsePolling()));
builder.Services.AddTriviumHealthChecks();
var host = builder.Build();
await host.RunAsync();
```

- One supervisor with one loop per polling bot, or one `BackgroundService` per bot; the implementation must isolate
  failures so one bot's loop cannot kill another.
- `ReceiverOptions` controls `AllowedUpdates`, `Limit`, `Offset`, `DropPendingUpdates`.
- Optional dedup runs through the shared ingress filter when `Trivium.Deduplication` is registered. Without it, polling
  is at-least-once: the offset is the primary acknowledgement and handlers must be idempotent.
- Backpressure is pull-based: pause `getUpdates` while the in-flight budget is exhausted; never buffer without bound.
- Exponential backoff with jitter on transient errors; fail fast on invalid token (401) and conflict (409).
- Graceful shutdown stops intake, drains in-flight work, then disposes.
- Health reports loop liveness, last successful update and last error.

### Webhooks (`Trivium.AspNetCore`)

```csharp
var app = builder.Build();
app.MapTriviumWebhook("/trivium/telegram/{botId}");
app.Run();
```

- Resolve the bot from `{botId}`; unknown bot → 404.
- Validate `X-Telegram-Bot-Api-Secret-Token` with a constant-time comparison.
- Enforce body size and JSON depth; deserialize with source-generated `System.Text.Json`.
- Deduplicate `(bot_id, update_id)` through the ingress filter before dispatch when `Trivium.Deduplication` is
  registered. A completed duplicate returns `200 OK`; an in-progress duplicate returns `409 Conflict` so Telegram retries
  later. The filter never waits for the other delivery.
- The handler token carries host shutdown and the handler timeout, not `RequestAborted`: a transport timeout must not
  cancel work whose reply goes out through `ITelegramBotClient`.
- Ack mode:
  - `AfterProcess` (default): 200 after the handler completes; 5xx on failure so Telegram retries.
  - `AfterEnqueue`: bounded channel + 200; opt-in, documented as lossy on crash.
- Backpressure:
  - `AfterEnqueue`: a full bounded queue returns `503 Service Unavailable`; Telegram retries non-2xx on its own
    schedule and `Retry-After` is advisory. Release the dedup entry before returning so the retry is not treated as a
    duplicate.
  - `AfterProcess`: use an optional concurrency limit and return `503` when saturated instead of queuing without bound.
  - Memory is never unbounded.
- Webhook management: opt-in `SetWebhook`/`DeleteWebhook`/`GetWebhookInfo` with secret, allowed updates, max
  connections and drop-pending options.
- The endpoint works in minimal APIs and controller-based hosts; it composes with host auth/rate limiting/OpenAPI.

## Persistence boundary

```text
Trivium               IUpdateIngressFilter, IngressOutcome; no data access, no schema, no state
Trivium.Deduplication dedup filter and in-memory deduplicator: ephemeral coordination state
Trivium.Hosting       registers ingress filters
Application           schema, migrations, stores, conversation state, shared dedup backend
```

- The framework never opens a database connection. There is no schema, no store package and no migration tool in the
  distribution.
- Dedup is coordination, not storage: it holds a bounded set of in-flight and recently handled update ids, and a shared
  backend implements the same contract in application code.
- Conversation state is application code: a store, a schema and a guard. `samples/Trivium.Sample.Persistence` is the
  reference.
- Guards keep the seam honest: a state gate is an `IUpdateGuard`, so the framework never needs to know what a flow is.
- Filter semantics and the dedup key layout: [Deduplication](../reference/deduplication.md).

## Error handling

```csharp
public interface IUpdateErrorHandler
{
    ValueTask HandleAsync(TriviumContext ctx, Exception exception, CancellationToken ct);
}
```

Classification:

| Exception                                        | Treatment                                                                          |
| ------------------------------------------------ | ---------------------------------------------------------------------------------- |
| `OperationCanceledException` (host/update token) | Not an error; log at debug; do not retry.                                          |
| `ApiRequestException` (Telegram)                 | Log status/description, never the token; surface retry-after where available.      |
| `GuardDeniedException`                           | Handled by the denial policy, not the error handler.                               |
| Application data access exception                | Configurable retry for idempotent reads; writes rely on upserts.                   |
| Handler exception                                | Log with route/handler/bot/update; return 5xx in webhook mode if not acknowledged. |
| Unknown exception                                | Log with full exception; never crash the loop.                                     |

No `async void`. No fire-and-forget without an explicit supervisor and error path.

## Observability

- `ActivitySource "Trivium"` with one activity per update and child activities for route match, guards, handler, data
  access and Telegram calls.
- `Meter "Trivium"` with counters/histograms for received, processed, failed, duration, queue depth, dedup hits and
  data access latency.
- Tags are bounded: bot id/name, update kind, route name, handler name, outcome, transport. Never chat id, user id,
  message text or callback data as metric labels.
- Logs use `LoggerMessage` and scopes: `BotId`, `BotName`, `UpdateId`, `UpdateKind`, `Route`.
- Redaction is enforced by tests.
- `Trivium.OpenTelemetry` registers instrumentation; exporters are the host's choice.

## AOT and trimming

- No reflection in dispatch, routing, callback codecs or DI registration.
- `System.Text.Json` source-gen for every Trivium-owned payload (callback data, webhook update envelope where needed).
- No `dynamic`, no runtime expression compilation.
- `IsAotCompatible=true`, `IsTrimmable=true`, trim analyzers on.
- DI uses explicit registrations generated at compile time where possible; keyed services are supported.
- Native AOT publish validation is deferred beyond 1.0, but the architecture must not require reflection.

## Security model

- **Webhook:** secret token required in production; constant-time comparison; optional IP allowlist as
  defense-in-depth; body/depth limits; no payload echo in errors.
- **Tokens:** configuration/user-secrets only; never logged, traced, returned by health or included in exceptions.
- **Callback data:** untrusted; versioned, size-limited, typed decoding; malformed data is a handled failure.
- **SQL:** the framework issues none; application SQL is parameterized and runs under a least-privilege role.
- **PII:** store only what is needed; document retention; redact logs/metrics by default.
- **Mini Apps:** `initData` validation helper with HMAC and expiry (M7/M8).
- **Dependencies:** scanning and update cadence in CI.

## Versioning and compatibility

- There is no public release before 1.0. The public API is frozen at 1.0 and follows SemVer afterwards.
- The public API snapshot is reviewed at every milestone; changes before 1.0 are allowed but intentional.
- `[Obsolete]` APIs get at least one minor release of notice and an analyzer warning after 1.0.
- Generated code is not part of the public API contract; only the attributes and runtime types are.

## Deliberate simplifications

These are choices, not omissions:

- One handler per update. Fan-out is not supported; compose handlers explicitly when one update has multiple concerns.
- No runtime plugin loading or assembly scanning at startup beyond the generated registration.
- No built-in queue/outbox in 1.0.
- No database layer: schema, migrations, stores and conversation state belong to the application.
- No high-level payment/gift/story domain model.
