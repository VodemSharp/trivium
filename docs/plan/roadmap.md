---
title: Roadmap & Milestones
last_reviewed: 2026-11-21
---

# Trivium — Roadmap & Milestones

[← Back to AGENTS.md](../../AGENTS.md)

**Related:** [Docs index](../README.md) · [Architecture](../fundamental/architecture.md) ·
[Deduplication](../reference/deduplication.md) · [Quality gates](../fundamental/quality.md) ·
[Telegram coverage](../reference/telegram-coverage.md) · [Diagnostics](../reference/diagnostics.md)

## How to read this plan

- There are **no calendar estimates**. The project is explicitly completeness-driven, not schedule-driven.
- Each milestone is an **outcome**, not a task list. The task lists are exhaustive enough to execute, but the exit
  criteria are the contract.
- **Scope size** is relative effort only: `S` (small), `M` (medium), `L` (large), `XL` (very large). It is not a
  duration.
- A milestone is not "done" until its exit criteria and the
  [Global definition of done](#Global%20definition%20of%20done) both pass.
- There is **one public release**: 1.0, published only when every milestone is complete. Nothing is released
  mid-plan, so milestones are not version boundaries.
- No milestone is closed with a known P0/P1 defect or a disabled gate.

## Locked decisions

These decisions shape every milestone. Changing one requires an explicit review and a docs update in the same change.

| Area                | Decision                                                                                                                                                                                                                                               |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Runtime             | .NET 10 only (`net10.0`).                                                                                                                                                                                                                              |
| Bot API             | `Telegram.Bot 22.10.3.3` / Bot API 10.3 baseline; track newer 22.x releases with a compatibility matrix.                                                                                                                                               |
| API shape           | Attributes + incremental source generator are the only route-authoring model; the generated `TriviumRouteTable` is registered explicitly. No runtime reflection in routing, dispatch, callback data or DI registration.                                |
| Handlers            | Closed signature set: `TriviumContext`, one typed payload, one `CancellationToken`, `CommandArguments`, `[CallbackData]`. Returns `Task`/`Task<T>`/`ValueTask`/`ValueTask<T>`.                                                                         |
| Routing             | Update kind → exact → longest parameterized prefix (word boundary) → default. One handler per update; fan-out is not supported, so multiple concerns compose explicitly.                                                                               |
| Guards              | A denied route stops. It never falls through to another route, a default route or an unfiltered list.                                                                                                                                                  |
| Core                | Stores nothing. No users, state, schema or database types in `Trivium`; ingress filters are the seam, not a policy.                                                                                                                                    |
| Hosting             | `Trivium.Hosting` (DI, polling, multi-bot) and `Trivium.AspNetCore` (webhook) are separate packages.                                                                                                                                                   |
| Multi-bot           | First-class: keyed DI, immutable registry, per-bot routes, transports, commands and data partitions.                                                                                                                                                   |
| Persistence         | Application-owned. No schema, no stores, no migrations and no state API ship with the framework. PostgreSQL for durable data and Redis-compatible servers for shared dedup are demonstrated in the persistence sample.                                 |
| Dedup               | Policy in its own package: `Trivium.Deduplication` ships `IUpdateDeduplicator`, the filter, an in-memory implementation and `UseDeduplication()`. Core exposes only `IUpdateIngressFilter` and `IngressOutcome`. A shared backend is application code. |
| Local orchestration | Aspire AppHost with PostgreSQL and Redis for the persistence sample; Aspire.Hosting.Testing for integration tests.                                                                                                                                     |
| Authorization       | Not provided. Trivium evaluates guards but ships no role or permission model; applications implement `IUpdateGuard` against their own principal.                                                                                                       |
| Callback data       | Typed `[CallbackData]` codecs with a version byte and a 64-byte runtime guard; raw string paths remain supported.                                                                                                                                      |
| Webhooks            | Secret validation in 1.0; the ingress verdict makes transports dedup-agnostic: `Acknowledge` → 200, `Defer` → 409. Durable outbox is deferred.                                                                                                         |
| AOT                 | AOT/trimming-safe design from day one; full Native AOT publish validation is deferred until after the plan.                                                                                                                                            |
| Packages            | Fine-grained: `Trivium`, `Trivium.Deduplication`, `Trivium.Hosting`, `Trivium.AspNetCore`, `Trivium.Generators`, `Trivium.OpenTelemetry`, `Trivium.Testing`, `Trivium.Templates`.                                                                      |
| Process             | Repository conventions: `.slnx`, central packages, warnings as errors, banned APIs, xunit.v3/MTP, GitHub Actions, XML docs + VitePress + DocFX.                                                                                                        |
| Time                | `TimeProvider` and `DateTimeOffset` everywhere.                                                                                                                                                                                                        |
| License             | MIT.                                                                                                                                                                                                                                                   |

## Global definition of done

Every milestone must satisfy all of these before it is closed:

1. `dotnet build Trivium.slnx` is **0 warnings** with `TreatWarningsAsErrors=true`.
2. `dotnet format Trivium.slnx --verify-no-changes` is clean.
3. New public APIs have XML documentation; the public-API snapshot is updated intentionally.
4. Unit tests cover the new behavior; integration tests cover the new infrastructure; generator changes have snapshot
   and analyzer tests.
5. Every new `CancellationToken` is flowed; every new I/O call is cancellable.
6. No new `DateTime.Now/UtcNow`, `Task.Result`, `Thread.Sleep`, `new HttpClient()`, `async void`, Newtonsoft.Json or
   runtime reflection in a dispatch path (enforced by banned symbols + IL scan).
7. Logs are structured, scoped (`BotId`, `UpdateId`) and contain no message text, tokens or user PII by default.
8. Docs are updated in the same change: fundamental guide, reference matrix and schema/versioning notes where relevant.
9. Package output is deterministic, symbol-bearing, XML-doc-bearing and passes `dotnet pack` validation.
10. The change is reviewed against the [security checklist](../fundamental/quality.md#Security%20checklist) when it
    adds network, persistence or auth surface.

## Dependency graph

```text
M0 Foundation
  └─ M1 Kernel
       ├─ M2 Generator
       │    └─ M3 Hosting ── M4 Webhook
       │             └─ M5 Deduplication ── M6 Persistence sample
       └─ M7 Telegram coverage
                 └─ M8 Observability & resilience
                          └─ M9 Samples, templates, docs
                                   └─ M10 Release readiness
```

M2 can start once M1's route model is stable. M5 needs M4's ingress verdict and M1's `BotId`/`UpdateId` value objects.
M7 can start once M1's update-kind matrix exists and continue in parallel with M2–M6. M8 needs M3 and M4. M9 needs
everything user-visible. M10 is the freeze.

---

## M0 — Foundation

**Goal:** a buildable, testable, analyzable, package-ready empty framework with CI and documentation structure.

**Depends on:** nothing. **Scope size:** S.

**Status:** complete (2026-11-21). Deferred by design, not by omission: NuGet lock files until the dependency graph
stabilizes, coverage collection until the kernel has behavior worth measuring (after M1), Aspire-backed integration
tests until the sample infrastructure exists in M6, and the DocFX API reference until M9.

**Non-goals:** no routing, no bot API code, no data layer.

### Workstreams

#### 0.1 Repository and build

- [x] Create the solution structure from the [repository layout](../README.md#Repository%20layout): all package
      projects, app projects, test projects, samples.
- [x] `Trivium.slnx` with solution folders; no `.sln`.
- [x] `global.json`: .NET SDK `10.0.4xx`, `rollForward: latestFeature`,
      `"test": { "runner": "Microsoft.Testing.Platform" }`.
- [x] `Directory.Build.props`:
  - `TargetFramework=net10.0`, `LangVersion=latest`, `Nullable=enable`, `ImplicitUsings=enable`.
  - `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`, `AnalysisMode=Recommended`.
  - `GenerateDocumentationFile=true`, `NoWarn` only for justified library-wide cases.
  - `IsAotCompatible=true` on libraries; `IsTrimmable=true`; `EnableTrimAnalyzer=true`.
  - `ContinuousIntegrationBuild`, `Deterministic`, `EmbedUntrackedSources`, SourceLink.
- [x] `Directory.Build.targets` (in-house analyzers/generator wiring) and `Directory.Packages.props` with central
      package management and pinned versions:
      `Telegram.Bot 22.10.3.3`, `Microsoft.Extensions.* 10.0.x`, `Aspire.Hosting.*`, `xunit.v3`, Roslyn, Roslynator,
      AsyncFixer, VSTHRD, BannedApi. Database packages (`Npgsql`, `Dapper`, `Dapper.AOT`, `DbUp.PostgreSQL`,
      `StackExchange.Redis`) are pinned centrally and referenced only by samples.
- [x] `.editorconfig` with the house conventions: `ct` naming, block bodies, line length 120, file-scoped namespaces,
      one type per file, no implicit `var` where unclear, LF endings.
- [x] `BannedSymbols.txt`: `DateTime.Now/Today/UtcNow`, `DateTimeOffset.Now/UtcNow`, `Task.Wait*`, `Task.Result`,
      `ValueTask.Result`, `Thread.Sleep`, `new HttpClient()`, `Newtonsoft.Json` entry points, `async void` is handled by
      analyzers/diagnostics.
- [x] `.config/dotnet-tools.json` for local tools (ReSharper CLI, etc.); `dotnet tool restore` in CI.
- [x] `NuGet.config`, `.gitignore`, `.gitattributes`, `LICENSE` (MIT), `README.md`, `SECURITY.md`,
      `CONTRIBUTING.md`, issue/PR templates.
- [x] Versioning: fixed `1.0.0-dev` during development; the release tag `v1.0.0` produces `1.0.0`. No hand-edited release versions.
- [x] Package metadata: `PackageId`, `Description`, `RepositoryUrl`, `PackageLicenseExpression=MIT`,
      `PackageReadmeFile`, `PackageIcon`, `IncludeSymbols`, `SymbolPackageFormat=snupkg`, `PackageTags`.

#### 0.2 Quality tooling

- [x] Analyzer packages referenced once in `Directory.Build.props`: Roslynator, AsyncFixer, VSTHRD,
      `Microsoft.CodeAnalysis.BannedApiAnalyzers`.
- [x] In-house `Trivium.Generators/Analyzers` project applied via `Directory.Build.targets`; diagnostic IDs reserved
      `TRIVIUM0001+`.
- [x] Test projects: `xunit.v3` only (no VSTest, no `coverlet.collector`); `Microsoft.Testing.Platform` runner.
- [x] `dotnet format` and `jb inspectcode` configuration baselined; no ReSharper noise.
- [ ] Coverage collection via Microsoft.Testing.Platform extensions (not Coverlet); line and branch reports —
      deferred until the kernel has behavior worth measuring (after M1).

#### 0.3 CI

- [x] GitHub Actions workflow `ci.yml`:
  - restore (NuGet lock files arrive when the dependency graph stabilizes);
  - build `-c Release`;
  - `dotnet format --verify-no-changes`;
  - unit + generator + analyzer tests;
  - integration tests (Aspire AppHost, Linux) — deferred to M6, when the AppHost and containers exist;
  - `dotnet pack` all packages;
  - public API snapshot validation.
- [x] CodeQL, dependency review, secret scanning.
- [x] Docs link checker and markdown lint.

#### 0.4 Docs skeleton

- [x] `docs/README.md`, `docs/fundamental/`, `docs/reference/`, `docs/plan/`.
- [x] Doc site scaffold: VitePress for guides; DocFX API reference is scaffolded in M9.
- [x] ADR-0001 (Trivium is a framework, not a bot API wrapper) is captured by the
      [architecture goals and non-goals](../fundamental/architecture.md#Goals); no separate ADR file.
- [x] Capture the working agreement in `AGENTS.md` (git approval-gated; no commits without explicit approval).

### Deliverables

- Empty but complete solution with all packages producing `.nupkg` + `.snupkg` + XML docs.
- Green CI on the skeleton.
- Docs site builds locally.
- Architecture goals/non-goals (ADR-0001) and the locked-decisions table.

### Exit criteria

- [x] `dotnet build Trivium.slnx -c Release` → 0 warnings, 0 errors.
- [x] `dotnet test Trivium.slnx` runs the (empty) suites successfully.
- [x] `dotnet pack` produces every intended package with README, icon, XML docs and symbols.
- [x] CI is green on a clean clone, including format, analyzers and docs.
- [x] `dotnet new` template placeholder exists and builds (content can be minimal).

### Risks / watchouts

- Analyzer churn can stall progress. Mitigation: adopt rules family-by-family, binary severity (`none`/`warning`), and
  fix all findings in the same change.
- Central package management can hide transitive conflicts. Mitigation: `dotnet list package --include-transitive` in
  CI and a dependency-update bot.
- Public library XML docs are mandatory even though application code often omits them. This is a deliberate
  deviation documented in [Quality gates](../fundamental/quality.md).

---

## M1 — Core routing kernel

**Goal:** deterministic, allocation-conscious, reflection-free dispatch of any Telegram update to one handler, with
explicit errors and a testable context.

**Depends on:** M0. **Scope size:** L.

**Non-goals:** no source generator yet (tests construct the route table directly from the contract), no hosting, no data
layer, no authorization.

### Workstreams

#### 1.1 Value objects and core types

- [ ] `BotId`, `UserId`, `ChatId`, `MessageId`, `UpdateId`, `RouteId` as `readonly record struct` with validation and
      explicit conversions to/from `long`/`int`.
- [ ] `UpdateKind` enum mirroring every `Telegram.Bot.Types.Enums.UpdateType` value plus `Unknown`; a test asserts the
      mapping against the library enum.
- [ ] `Actor` (`UserId`, `User?`, `ChatId?`, `Chat?`, `IsBot`, `LanguageCode`) resolved uniformly.
- [ ] `UpdateEnvelope`: `BotId`, `Update`, `Kind`, `Actor`, `Chat`, `ReceivedAt`, `RawJson?`, `Source` (polling/webhook).
- [ ] `TriviumContext`: bot client, bot id, update, kind, actor, chat, services, cancellation token, route, items,
      typed accessors (`Message`, `CallbackQuery`, `InlineQuery`, `ChatMemberUpdated`, …), reply/send/edit/answer
      helpers.
- [ ] `CommandArguments`: raw text, parsed values (quote-aware), `TryGet<T>` for primitives, `Empty`.
- [ ] `TriviumException` hierarchy: routing, guard, invocation, configuration.

#### 1.2 Route model

- [ ] `Route` hierarchy: `MessageRoute` (exact/parameterized/default), `CallbackRoute`, `InlineRoute`,
      `UpdateRoute` (any supported `UpdateKind`), `FallbackRoute`.
- [ ] Immutable `RouteTable` built at startup; no mutation after build.
- [ ] `RouteMatcher` with documented precedence:
  1. update kind must match;
  2. exact text / exact callback path;
  3. longest literal prefix for parameterized routes;
  4. default route;
  5. no match.
- [ ] Command mention normalization: `/start@MyBot` matches `/start` when the mention is this bot's username.
- [ ] Case sensitivity configurable per route; default ordinal case-sensitive.
- [ ] Prefix matching requires a separator boundary (`/start` does not match `/startle`); argument tail is trimmed.
- [ ] Caption fallback for media messages, opt-in per route.
- [ ] Startup conflict detection: duplicate exact routes, duplicate callback paths, ambiguous prefixes, multiple
      defaults, multiple catch-alls.
- [ ] `RouteMatch` carries the matched route, captured arguments and match quality; never `Single()`.

#### 1.3 Dispatch pipeline

- [ ] `IUpdateDispatcher.DispatchAsync(BotId, Update, CancellationToken)`.
- [ ] Per-update `IServiceScope` created from `IServiceScopeFactory`; scoped services resolved inside.
- [ ] Pipeline: resolve actor → match route → run guards → run before-middleware → invoke handler → run
      after-middleware → error handling.
- [ ] `IUpdateGuard` with `ValueTask<GuardResult>`; `GuardResult.Allow/Deny(reason)`.
- [ ] `IUpdateMiddleware` with `UpdateDelegate` (`ValueTask InvokeAsync(TriviumContext, UpdateDelegate next)`).
- [ ] `IHandlerInvoker` abstraction; the M1 implementation uses strongly typed delegates (no `MethodInfo`).
- [ ] `IUpdateErrorHandler`; default logs with `BotId`, `UpdateId`, route and exception; `OperationCanceledException`
      is not logged as an error.
- [ ] `IUpdateIngress` runs optional `IUpdateIngressFilter` instances before dispatch; dedup is one filter, not a
      transport-specific step.
- [ ] No `async void`, no blocking `.Result`/`.Wait()`, no fire-and-forget.
- [ ] One route executes per update; multiple matches are rejected at startup.

#### 1.4 Actor and chat resolution

- [ ] Resolve actor for all update kinds that have one: `Message`, `EditedMessage`, `ChannelPost`,
      `EditedChannelPost`, `CallbackQuery`, `InlineQuery`, `ChosenInlineResult`, `ShippingQuery`,
      `PreCheckoutQuery`, `PollAnswer`, `ChatMember`, `MyChatMember`, `ChatJoinRequest`, business messages,
      reactions, boosts, paid media, guest and managed-bot updates.
- [ ] Resolve chat for all chat-bearing updates, including business messages and channel posts.
- [ ] No update kind silently produces a null actor if one exists; table-driven tests.
- [ ] Sender extraction is an explicit resolver, not a helper that only understands messages.

#### 1.5 Route table contract

The immutable table the generator emits into and tests populate. It is data, not an authoring API; routes are authored
with attributes in M2.

- [ ] Public `TriviumRouteTable` and route descriptor types: exact-match dictionary, sorted prefix array, callback
      dictionary, defaults, per-bot scopes.
- [ ] Descriptors carry route metadata: name, description, order, bot scope, tags (for docs/command catalog).
- [ ] Construction is deterministic (stable ordering) and validates on build; no mutation after build.
- [ ] Validation errors are descriptive and point at the route name.
- [ ] `Trivium.Testing` populates the same types, so tests dispatch a small table without a generator.
- [ ] No `Delegate` + `DynamicInvoke` anywhere.

#### 1.6 Tests

- [ ] Table-driven routing tests: exact, prefix, longest-prefix, boundary, case, mention, caption, default, no match,
      duplicate, ambiguous.
- [ ] Dispatch tests: one route, zero routes, multiple matches rejected at startup, cancellation, exception, guard
      denial, middleware ordering.
- [ ] Actor resolution matrix covering every update kind.
- [ ] Allocation tests: no-match path allocates below a documented budget.
- [ ] Architecture test: core has no reference to hosting, ASP.NET Core or a database driver.

### Deliverables

- `Trivium` kernel with public API and XML docs.
- `Trivium.Testing` skeleton: fake `ITelegramBotClient`, `UpdateBuilder`, `TriviumTestHost`.
- Routing documented in [Routing rules](../fundamental/architecture.md#Routing%20rules).

### Exit criteria

- [ ] A fake update can be dispatched end-to-end from a console test host.
- [ ] 100% of documented routing rules have a passing test, including the old library's failure cases:
      multiple matches, prefix ambiguity, default fallback, guard bypass.
- [ ] No reflection in `Trivium` dispatch (IL scan test).
- [ ] Public API review: names, nullability, `ValueTask` vs `Task`, cancellation tokens.
- [ ] No unexplained allocations in the no-match path.

### Risks / watchouts

- Over-abstracting the pipeline. Mitigation: only three extension points in M1 — guard, middleware, error handler.
- Matching semantics are easy to get subtly wrong. Mitigation: write the rules doc first, then tests, then code.
- `UpdateKind` drift against Telegram.Bot. Mitigation: reflection-based test over `Enum.GetValues<UpdateType>()` is
  allowed in tests only.

---

## M2 — Source generator, attributes and diagnostics

**Goal:** attribute-first handlers compile into a zero-reflection route table, invokers, DI registrations, command
catalog and callback-data codecs, with build-time diagnostics for every routing mistake.

**Depends on:** M1. **Scope size:** XL.

**Non-goals:** no runtime Roslyn, no interceptors, no `Reflection.Emit`, no source-generated Bot API wrappers.

### Workstreams

#### 2.1 Attributes

- [ ] Generic `[UpdateHandler(UpdateKind kind)]` for any update kind.
- [ ] Convenience attributes that forward to it:
      `[MessageCommand]`, `[EditedMessageCommand]`, `[ChannelPostCommand]`, `[EditedChannelPostCommand]`,
      `[CallbackQuery]`, `[InlineQuery]`, `[ChosenInlineResult]`, `[ShippingQuery]`, `[PreCheckoutQuery]`,
      `[Poll]`, `[PollAnswer]`, `[ChatMember]`, `[MyChatMember]`, `[ChatJoinRequest]`,
      `[BusinessConnection]`, `[BusinessMessage]`, `[EditedBusinessMessage]`, `[DeletedBusinessMessages]`,
      `[MessageReaction]`, `[MessageReactionCount]`, `[ChatBoost]`, `[RemovedChatBoost]`,
      `[PurchasedPaidMedia]`, `[GuestMessage]`, `[ManagedBot]`, `[Subscription]`,
      `[StoppedMessageGeneration]`, and `[UpdateHandler]` (catch-all when no kind is specified; with a kind it targets
      that update).
- [ ] Route metadata: `Text`, `Description`, `Order`, `Bot`, `IgnoreCase`, `AllowCaption`, `Hidden`.
- [ ] `[CommandGroup("admin")]` for class-level composition (prefix/metadata; not a runtime group object).
- [ ] `[Bot("main")]` for per-bot route scoping; absence means all bots.
- [ ] `[CallbackData]` for partial records/classes.
- [ ] Guard attributes are recognized by the generator but defined by the application; the framework ships no guard
      that reads external data.

#### 2.2 Handler shapes

Support exactly these shapes, and reject everything else with a diagnostic:

```csharp
// Static method
[MessageCommand("/start")]
public static Task Start(TriviumContext ctx, CancellationToken ct);

// Static method with typed payload
[CallbackQuery("menu:settings")]
public static ValueTask Settings(TriviumContext ctx, SettingsCallback data, CancellationToken ct);

// Instance handler class (scoped by default)
[CommandGroup("admin")]
public sealed class AdminCommands
{
    [MessageCommand("/ban")]
    public ValueTask Ban(TriviumContext ctx, CommandArguments args, CancellationToken ct);
}
```

- [ ] Supported parameters: `TriviumContext`, the typed update payload (`Message`, `CallbackQuery`, `InlineQuery`,
      `ChatMemberUpdated`, …), `CancellationToken`, `CommandArguments`, and `[CallbackData]` payloads.
- [ ] Supported return types: `Task`, `Task<T>` (result ignored or logged), `ValueTask`, `ValueTask<T>`.
- [ ] No `void`, no `async void`, no `ref`/`out`, and at most one parameter per slot; a duplicate is a diagnostic.
- [ ] Instance handlers are registered with the generator-selected lifetime (default scoped, overridable).
- [ ] The generator is the only signature validator. There is no delegate-based registration path, so no second
      validation surface exists.

#### 2.3 Generated outputs

- [ ] `TriviumRouteTable.g.cs`: immutable route table, exact dictionary, sorted prefix array, callback dictionary,
      defaults, per-bot scopes.
- [ ] `TriviumHandlerInvoker.g.cs`: direct method-group calls; instance handlers resolved via
      `GetRequiredService<THandler>()`.
- [ ] `TriviumServiceMap.g.cs`: DI registrations for handler classes and generated services.
- [ ] `TriviumCommandCatalog.g.cs`: command catalog (text, description, scope, language, hidden) for `SetMyCommands`.
- [ ] `TriviumCallbackDataSerializer.g.cs`: `Encode`/`TryDecode` + `JsonSerializerContext` per `[CallbackData]` type.
- [ ] `TriviumAllowedUpdateSet.g.cs`: the minimal `AllowedUpdates` set derived from registered routes, per bot.
- [ ] Registration is a single explicit call: `AddTrivium<TriviumRouteTable>(...)`, no scanning, no generated extension
      method and no assembly attribute read by reflection; a missing or mismatched table fails at startup.
- [ ] Generated type names are singular and describe the artifact they own; no plural container classes.
- [ ] Generated code is deterministic (stable ordering, no timestamps, no machine paths).
- [ ] Generated code is annotated `[GeneratedCode]` and excluded from coverage/analyzers where appropriate.

#### 2.4 Diagnostics and analyzers

| ID            | Severity | Rule                                                                                      |
| ------------- | -------- | ----------------------------------------------------------------------------------------- |
| `TRIVIUM0001` | Error    | Duplicate route (same kind + exact key + bot scope)                                       |
| `TRIVIUM0002` | Error    | Ambiguous parameterized prefixes (tie for longest)                                        |
| `TRIVIUM0003` | Error    | Multiple default/catch-all routes for the same kind + bot                                 |
| `TRIVIUM0004` | Error    | Unsupported handler return type                                                           |
| `TRIVIUM0005` | Error    | Unsupported handler parameter                                                             |
| `TRIVIUM0006` | Error    | `[CallbackData]` type is not `partial` / unsupported shape                                |
| `TRIVIUM0007` | Warning  | Callback payload can exceed 64 bytes (constant data)                                      |
| `TRIVIUM0008` | Error    | `[MessageCommand]` on a non-message update kind                                           |
| `TRIVIUM0009` | Error    | Handler class is static but has instance handlers, or vice versa                          |
| `TRIVIUM0010` | Warning  | Route has no description and is not hidden (command catalog quality)                      |
| `TRIVIUM0011` | Error    | `[Bot]` name is empty or invalid (unknown bot names are startup errors, not compile-time) |
| `TRIVIUM0012` | Error    | More than one `CancellationToken` parameter                                               |
| `TRIVIUM0013` | Error    | Handler type is not accessible to generated code (private/nested/inaccessible)            |

House-style analyzers `TRIVSTYLE0001`–`TRIVSTYLE0005` are listed in
[in-house analyzers](../fundamental/quality.md#In-house%20analyzers) and in the
[diagnostics reference](../reference/diagnostics.md).

- [ ] Analyzer fixes for the trivial cases: add `partial`, remove unsupported parameter, make method static/instance,
      add description.
- [ ] Diagnostics are documented in `docs/reference/diagnostics.md`.
- [ ] Review the diagnostic table before M2 exits; IDs and severities are provisional until each rule lands.
- [ ] Analyzer tests via `Microsoft.CodeAnalysis.Testing` or the in-house analyzer test harness.

#### 2.5 Callback data

- [ ] `[CallbackData]` partial record/class; generated compact field names via `[JsonPropertyName]`; a version byte is
      reserved.
- [ ] Encoder/decoder uses `System.Text.Json` source generation, no reflection, no Newtonsoft.
- [ ] Runtime size check: encoded value must be ≤ 64 bytes; failure is a typed `CallbackDataTooLargeException` with the
      encoded length.
- [ ] Decode failure is a typed result; malformed data never crashes the dispatcher and can be routed to a fallback.
- [ ] Raw string callback paths remain first-class for small/static keyboards.
- [ ] Unit tests: round-trip, Unicode, max size, version mismatch, malformed input, unknown fields.

#### 2.6 Generator quality

- [ ] Incremental generator only (`IIncrementalGenerator`), equatable models, no `Microsoft.CodeAnalysis.Workspaces`
      reference in the generator assembly.
- [ ] Generator targets `netstandard2.0`; the shipping package contains the generator under `analyzers/dotnet/cs`.
- [ ] No generation when unrelated code changes (incremental cache test).
- [ ] Generator works with `dotnet build`, IDE design-time builds and `dotnet publish` (AOT/trimming).
- [ ] Snapshot tests for generated source; approval tests for diagnostics.
- [ ] Performance test: 1,000 routes generate in under a documented threshold and do not regress >20%.

#### 2.7 AOT/trimming

- [ ] `IsAotCompatible=true` on all libraries.
- [ ] No `MethodInfo.Invoke`, `DynamicInvoke`, `Activator.CreateInstance`, `MakeGenericType` in generated or core code.
- [ ] STJ source-gen contexts for callback data and any Trivium-owned JSON.
- [ ] Trim analysis clean on the sample app (`PublishTrimmed=true`).
- [ ] Full Native AOT publish is validated after 1.0, but the design must not block it.

### Deliverables

- `Trivium.Generators` package with the generator and its compile-time diagnostics.
- Attribute-first API and callback-data codec.
- Sample `Trivium.Sample.Polling` using only attributes (no route registration code at all).
- Diagnostics reference.

### Exit criteria

- [ ] The sample app's route table is generated and registered with one `AddTrivium<TriviumRouteTable>` call; no route
      registration code exists.
- [ ] IL scan proves no reflection call in the dispatch path of the sample (test-only reflection allowed).
- [ ] Every diagnostic has a test and a documentation entry.
- [ ] Generated code is deterministic across machines and builds.
- [ ] A 1,000-route synthetic project builds and dispatches within the documented budget.
- [ ] Callback-data round-trip and 64-byte enforcement are tested.
- [ ] Trim analysis is clean; Native AOT is explicitly deferred beyond 1.0.

### Risks / watchouts

- Source generators are hard to debug and can slow IDEs. Mitigation: incremental models, no semantic model retained,
  snapshot tests, a build-time perf test, and a route-table contract covered by its own tests.
- Attribute explosion. Mitigation: one generic `[UpdateHandler]` plus convenience attributes; generated docs table.
- Handler signature creep. Mitigation: a closed list of supported shapes; the generator rejects the rest.
- Callback data is a public wire format. Mitigation: version byte, explicit compatibility tests, never reuse a version.

---

## M3 — Hosting, DI and multi-bot lifecycle

**Goal:** run one or many bots in a .NET host, with polling, startup validation, command synchronization, health checks
and clean shutdown.

**Depends on:** M2. **Scope size:** L.

**Non-goals:** no webhook endpoint (M4), no deduplication (M5), no persistence sample (M6).

### Workstreams

#### 3.1 Options and DI

- [ ] `AddTrivium<TRouteTable>(Action<TriviumBuilder>)` on `IServiceCollection`, where `TRouteTable` is the generated
      `TriviumRouteTable`; the table is resolved once at startup and validated (missing or mismatched type is a startup
      error).
- [ ] `TriviumBuilder.AddBot(string name, Action<BotBuilder>)` and `AddBotsFromConfiguration(IConfiguration)`.
- [ ] `TriviumOptions`: default transport, shutdown timeout, allowed updates policy, telemetry switches.
- [ ] `BotOptions`: token, transport (`Polling`, `Webhook`, `None`), webhook options, command sync, rate limits,
      route scope.
- [ ] Configuration binding with `IOptions` validation; fail fast on missing token or invalid bot id.
- [ ] Expand environment variables and support `IOptions`-friendly secret providers; never log tokens.
- [ ] Keyed services: `ITelegramBotClient` keyed by bot name; `BotId` and `BotDescriptor` scoped.
- [ ] `IBotRegistry` immutable after startup; duplicate bot names/ids are startup errors. Adding/removing bots requires a
      restart (no hot reload in 1.0).
- [ ] `TriviumContext.Bot` and `TriviumContext.BotId` resolve from the current bot; no ambient static.

#### 3.2 Multi-bot

- [ ] Derive `BotId` from the token prefix; validate it is numeric and unique. Resolve the bot username at startup
      (`GetMe` or explicit configuration) and use it for `/command@username` normalization.
- [ ] Per-bot route scopes: routes marked `[Bot("name")]` only run for that bot; unmarked routes run for all.
- [ ] Per-bot `AllowedUpdates` from the generated set; polling requests only what is routed.
- [ ] Per-bot data partition (`bot_id`) is available to application code from scoped services; the framework itself never
      queries a store.
- [ ] Independent lifecycle: start/stop/restart one bot without touching others.
- [ ] No global mutable `Dictionary`; registry is read-only after build.
- [ ] Tests: two bots, distinct routes, distinct clients, concurrent updates.

#### 3.3 Polling

- [ ] `BotPollingService : BackgroundService` per bot (or one supervisor with per-bot loops).
- [ ] `StartReceiving` with `ReceiverOptions` (`AllowedUpdates`, `Limit`, `Offset`, `DropPendingUpdates`) and the
      `HandleErrorSource` overload.
- [ ] Cancellation linked to host shutdown and per-bot stop; no `async void`.
- [ ] Error handling: log, backoff (exponential with jitter), never spin on persistent 401/409/429.
- [ ] 409 conflict (another poller) is a clear startup/runtime error with operator guidance.
- [ ] Offsets are not persisted by Trivium; Telegram owns them. Document the implications.
- [ ] Optional dedup through `IUpdateIngressFilter` when `Trivium.Deduplication` is registered; without it, polling is
      at-least-once and handlers must be idempotent.
- [ ] Pull-based backpressure: pause `getUpdates` while the in-flight budget is exhausted; never buffer without bound.
- [ ] Graceful shutdown: stop receiving, drain in-flight updates up to a configurable timeout.
- [ ] Health check: polling loop alive, last successful `GetMe`/`GetUpdates` timestamp.

#### 3.4 Command synchronization

- [ ] Generated command catalog → `SetMyCommands` per bot, scope and language.
- [ ] Scopes: default, all private, all group, chat, chat administrators, chat member (per Telegram API).
- [ ] Localized descriptions from resources or explicit metadata; fallback to default language.
- [ ] Opt-in `CommandSyncMode`: `None`, `MissingOnly`, `Replace` (default `None` for safety).
- [ ] Do not delete manually managed commands unless `Replace` is explicitly chosen.
- [ ] Startup sync is best-effort and logged; failure does not stop the bot unless configured.

#### 3.5 Health, logging and shutdown

- [ ] `AddTriviumHealthChecks()`: bot registry and each bot's transport. Applications add their own checks for whatever
      stores they use.
- [ ] `LoggerMessage` source-generated logs with scopes `BotId`, `BotName`, `UpdateId`, `UpdateKind`, `Route`.
- [ ] Startup banner lists bots, transports, route count, generated package version — no secrets.
- [ ] Graceful shutdown order: stop intake → drain → dispose clients → flush telemetry.
- [ ] `IHostApplicationLifetime` integration; no `Environment.Exit` in library code.

#### 3.6 Sample

- [ ] `Trivium.Sample.Polling` console host with two bots (one real token via user-secrets, one optional).
- [ ] Demonstrates attributes, groups, guards, typed arguments and graceful shutdown.
- [ ] `appsettings.json` contains no secrets; `dotnet user-secrets` documented.

### Deliverables

- `Trivium.Hosting` package.
- Polling supervisor, bot registry, options, command sync, health checks.
- Multi-bot polling sample.

### Exit criteria

- [ ] Two bots run concurrently, route independently, and stop cleanly.
- [ ] Missing/invalid token fails at startup with an actionable message and no secret leakage.
- [ ] Route scoping is tested for every combination (global, bot-specific, wrong bot).
- [ ] Command sync is idempotent and opt-in; tests cover all scopes.
- [ ] Health endpoint reports per-bot status.
- [ ] No `async void`, no unobserved tasks, no `Task.Result` in the host path.

### Risks / watchouts

- Multi-bot route scoping can surprise users. Mitigation: explicit `[Bot]`, startup route inventory, docs and tests.
- Polling conflicts are operationally confusing. Mitigation: detect 409, explain, expose health.
- Keyed DI is easy to misuse. Mitigation: never expose raw keyed resolution to handlers; always use `TriviumContext`.

---

## M4 — Webhook hosting and reliability

**Goal:** a secure-by-default ASP.NET Core webhook endpoint that authenticates Telegram, applies the ingress verdict,
applies backpressure and processes updates reliably.

**Depends on:** M3. The ingress filter contract and the outcome type land in `Trivium` (§4.3); the webhook maps the
outcome to status codes without referencing deduplication. **Scope size:** L.

**Non-goals:** no durable outbox/queue (deferred beyond 1.0), no multi-region coordination.

### Workstreams

#### 4.1 Endpoint

- [ ] `MapTriviumWebhook(this IEndpointRouteBuilder, string pattern = "/trivium/telegram/{botId}")`.
- [ ] Per-bot route resolution from `{botId}` or an opaque path key; unknown bot → 404 without leaking registry details.
- [ ] `POST` only; reject other verbs.
- [ ] Request body limit (default 1 MB, configurable); reject oversized bodies with 413.
- [ ] `Content-Type: application/json` required.
- [ ] Deserialize with `System.Text.Json` source-gen (`UpdateJsonContext`); no Newtonsoft, no reflection fallback.
- [ ] Malformed JSON → 400 with a generic body (no stack traces, no payload echo).
- [ ] Request cancellation flows into dispatch.

#### 4.2 Secret validation

- [ ] Read `X-Telegram-Bot-Api-Secret-Token`; compare to the configured per-bot secret with
      `CryptographicOperations.FixedTimeEquals`.
- [ ] Missing secret configuration is a startup error in Production; in Development it is a loud warning with an
      opt-in override.
- [ ] Wrong/missing header → 401/403, logged with bot id and remote IP, never with the provided value.
- [ ] Secret rotation documented; old secret accepted for a configurable overlap window (optional, default off).
- [ ] Optional IP allowlist documented as defense-in-depth, not a replacement for the secret.
- [ ] Tests: correct, wrong, missing, empty, timing-safe comparison, rotated secret.

#### 4.3 Ingress filters and the acknowledgement verdict

- [ ] `IUpdateIngressFilter` in `Trivium`: runs before routing on every transport, returns `IngressOutcome`.
- [ ] `IngressOutcome`: `Continue` (dispatch), `Acknowledge` (handled elsewhere; stop and ack),
      `Defer` (cannot run now; stop and retry later).
- [ ] Filters run in registration order; the first non-`Continue` outcome ends the pipeline and produces
      `DispatchResult.Acknowledged` or `DispatchResult.Deferred`.
- [ ] Webhook mapping: `Acknowledge` → `200 OK`, `Defer` → `409 Conflict`. No transport references deduplication or any
      other filter implementation.
- [ ] Polling mapping: `Acknowledge` advances the offset; `Defer` stops the batch, backs off and re-reads.
- [ ] The handler token carries host shutdown and the handler timeout, not `RequestAborted`; a transport timeout must not
      cancel work whose reply goes out through `ITelegramBotClient`.
- [ ] Deduplication itself ships in M5 as `Trivium.Deduplication`; M4 tests the verdict with a stub filter.

#### 4.4 Acknowledgement and backpressure

- [ ] `AckMode`:
  - `AfterProcess` (default): return 200 only after the handler completes; failures return 5xx so Telegram retries.
  - `AfterEnqueue`: enqueue into a bounded channel, return 200; opt-in for high throughput.
- [ ] Bounded channel with configurable capacity; full queue → `503 Service Unavailable`, never unbounded growth.
- [ ] `Retry-After` is advisory only; Telegram retries non-2xx on its own schedule.
- [ ] A rejected enqueue releases the dedup entry (`AbandonAsync`, wired in M5) so the retry is not treated as a
      duplicate.
- [ ] `AfterProcess` uses an optional concurrency limit and returns `503` when saturated.
- [ ] Background dispatcher drains the channel with graceful shutdown.
- [ ] Handler timeout (configurable); cancellation propagates; timeout returns 5xx in `AfterProcess`.
- [ ] No update is acknowledged before dedup is recorded in the configured backend.
- [ ] Document the at-least-once model: handlers must be idempotent; dedup reduces but does not eliminate duplicates
      across crashes.

#### 4.5 Webhook management

- [ ] `UseWebhookManagement()` / options: `SetWebhook` on startup with URL, secret, `AllowedUpdates`,
      `DropPendingUpdates`, `MaxConnections`, optional certificate/IP.
- [ ] `DeleteWebhook` on shutdown only if explicitly configured (default: leave webhook registered).
- [ ] `GetWebhookInfo` health check: last error date/message surfaced without secrets.
- [ ] `WebhookUrl` must be HTTPS in Production; localhost/test environment exception documented.
- [ ] Do not overwrite manually managed webhooks unless `Replace` is configured.
- [ ] Tests against a fake Telegram HTTP handler: payload shape, secret, allowed updates, idempotency.

#### 4.6 Host integration

- [ ] `AddTriviumWebhook()` registers endpoint services; works with minimal APIs and controllers hosts.
- [ ] `MapTriviumWebhook` participates in route groups, auth policies (host-level), rate limiting and OpenAPI.
- [ ] Health endpoint includes webhook status per bot.
- [ ] `Trivium.Sample.Webhook` minimal API host.
- [ ] Integration tests with `WebApplicationFactory<Program>` and a stub ingress filter for both outcomes;
      deduplication coverage lands with M5.

### Deliverables

- `Trivium.AspNetCore` package.
- Ingress filter contract (`IUpdateIngressFilter`, `IngressOutcome`) and the webhook acknowledgement mapping.
- Webhook sample and integration suite.
- Operations doc: URL, secret, allowed updates, retries, 5xx behavior, 409 on deferral.

### Exit criteria

- [ ] A missing or invalid secret is rejected in every production configuration; the development override is explicit and
      logged.
- [ ] `Acknowledge` returns 200 without dispatch; `Defer` returns 409 without dispatch; neither re-runs the handler.
- [ ] A full queue returns 503 and does not grow memory without bound.
- [ ] Malformed/oversized requests are rejected without stack traces or payload echo.
- [ ] The sample processes a real Telegram webhook locally through a tunnel (documented) and via tests.
- [ ] No Newtonsoft.Json in the dependency graph.

### Risks / watchouts

- "Exactly once" is impossible over HTTP. Mitigation: document at-least-once, dedup, idempotent handlers, and a future
  outbox.
- `AfterEnqueue` can lose updates on crash. Mitigation: default `AfterProcess`; opt-in is documented with trade-offs.
- Secret rotation can lock out the bot. Mitigation: explicit overlap window and a runbook.

---

## M5 — Deduplication

**Goal:** ship deduplication as its own package, so `Trivium` stays policy-free while every bot still gets a correct
default. The in-memory implementation is the reference; a shared backend is application code implementing the same
contract.

**Depends on:** M1 (`BotId`, `UpdateId`) and M4 (`IUpdateIngressFilter`, `IngressOutcome`). **Scope size:** S.

**Non-goals:** no Redis or database dependency in the package, no waiting for an in-flight delivery, no durable outbox.

### Workstreams

#### 5.1 Package

- [ ] `src/Trivium.Deduplication` package: references `Trivium.Hosting`, packs its own nuspec, appears in `Trivium.slnx`
      and the package map.
- [ ] Architecture test: `Trivium` references no dedup type; `Trivium.AspNetCore` never references the dedup package.
- [ ] Public API documented, including which assembly owns each type.

#### 5.2 Contract

- [ ] `IUpdateDeduplicator`:
  - `TryBeginAsync(BotId, UpdateId, CancellationToken)` → `Accepted` / `Duplicate` / `InProgress`.
  - `CompleteAsync` / `AbandonAsync` for retry semantics.
- [ ] `TryBeginAsync` records an in-flight entry with a lease; an expired lease is reclaimable, `CompleteAsync` marks it
      done and keeps it for the window, `AbandonAsync` removes it.
- [ ] `DeduplicationOptions`: lease, window, capacity. Lease exceeds the handler timeout; window is at least Telegram's
      retry window.
- [ ] Update ids are scoped per bot; never global.

#### 5.3 Filter and registration

- [ ] `UpdateDeduplicationFilter : IUpdateIngressFilter`: `Accepted` → `Continue`, `Duplicate` → `Acknowledge`,
      `InProgress` → `Defer`.
- [ ] `AbandonAsync` on a rejected enqueue, cancellation and handler failure, so the retry is accepted rather than
      dropped.
- [ ] `UseDeduplication()` registers the filter and the in-memory deduplicator; idempotent. `UseDeduplication<T>()`
      supplies a shared backend.
- [ ] In-memory implementation: bounded, driven by `TimeProvider`, reset on restart, documented as single-process only.

#### 5.4 Tests

- [ ] Contract tests every implementation passes: concurrent duplicates, crash between begin and complete, lease
      takeover, abandon-then-retry, per-bot scoping, bounded memory.
- [ ] Webhook integration with the real filter: completed duplicate → 200, in-progress → 409, abandoned retry →
      dispatched.

#### 5.5 Documentation

- [ ] [Deduplication](../reference/deduplication.md): the ingress seam, the outcome mapping, contract and lease/window
      semantics, in-memory limits, shared-backend guidance.
- [ ] Package map, architecture graph and AGENTS.md stay in step.

### Deliverables

- `Trivium.Deduplication` package with contract, filter, in-memory implementation, options and registration.
- Contract tests and webhook integration coverage.

### Exit criteria

- [ ] `UseDeduplication()` needs no configuration and no server: duplicate deliveries within the window are not
      dispatched twice.
- [ ] `Trivium` contains no dedup type, and `Trivium.AspNetCore` contains no reference to deduplication.
- [ ] `Defer` from the filter produces 409 on the webhook and no offset advance on polling.
- [ ] The in-memory implementation is bounded and its numbers come from `TimeProvider`.

### Risks / watchouts

- Users assume in-memory dedup spans instances. Mitigation: the reference page and the package XML docs say
  single-process in the first paragraph.
- A shared backend diverges in semantics. Mitigation: contract tests run against every implementation.

---

## M6 — Persistence and the reference sample

**Goal:** prove, in one runnable sample, that application-owned persistence and a shared dedup backend compose with the
framework without living in it: the app owns the schema, the store, the guard and the Redis-compatible deduplicator.

**Depends on:** M5. **Scope size:** M.

**Non-goals:** no framework schema, no store package, no migrator package, no conversation-state API, no documented state
pattern in the framework docs.

### Workstreams

#### 6.1 Sample schema and migration

- [ ] `Migrations/0001_conversation_state.sql`: `app.conversation_states` keyed by
      `(bot_id, user_id, chat_id, state_key)` with `payload jsonb`, `version bigint`, `created_at`, `updated_at` and an
      index on `updated_at`.
- [ ] The sample owns the SQL: no `EmbeddedResource` in a package, no framework migrator.
- [ ] `--migrate` applies the scripts with DbUp, streams structured logs, sets the exit code and stops; no HTTP server.
- [ ] Advisory lock around the upgrade; journal table documented; forward-only policy stated in the sample README.
- [ ] `dotnet run -- --migrate` works outside Aspire with a connection string from configuration; no secrets in code.
- [ ] Tests: empty database → migrate; migrate twice → no-op; schema assertions.

#### 6.2 State store and guard

- [ ] `ConversationStateStore` with Dapper + `Dapper.AOT` and explicit SQL: get, upsert with `version`,
      compare-and-swap, delete.
- [ ] Typed payloads with source-generated `System.Text.Json`; a raw `JsonDocument` overload for application-owned
      payloads.
- [ ] `RequireStateAttribute : Attribute, IUpdateGuard`: denies a route until the flow exists. An ordinary guard; it is
      the reference for state gating.
- [ ] Multiple parallel flows via distinct `state_key` values; zero `chat_id` means user-global, a real id scopes to the
      chat or topic.
- [ ] No automatic expiry: a purge query on `updated_at` and the retention decision live in the sample README.
- [ ] Tests: CAS race, parallel flows, chat scoping, purge by `updated_at`, cancellation.

#### 6.3 Shared dedup backend

- [ ] `RedisUpdateDeduplicator` implementing `IUpdateDeduplicator` over `SET NX PX`, `GET`, `SET XX PX` and `DEL`; keys
      `trivium:dedup:{bot_id}:{update_id}`.
- [ ] Lease and window from `DeduplicationOptions`; the lease exceeds the handler timeout.
- [ ] `StackExchange.Redis` is referenced by the sample only.
- [ ] TLS and credentials from the connection string; never logged.
- [ ] Sample tests: concurrent duplicate delivery, lease takeover, abandoned key, TTL expiry — the same contract tests as
      M5.

#### 6.4 Aspire AppHost

- [ ] `samples/Trivium.AppHost` with PostgreSQL and Redis containers and the persistence sample.
- [ ] The sample's `--migrate` run is a one-shot resource: `WaitFor(db)`, then `WaitForCompletion(migration)` on the bot.
- [ ] Connection strings injected as references, not hardcoded; ephemeral containers in test mode.
- [ ] AppHost starts and stops cleanly; `aspire run` documented.

#### 6.5 Sample documentation

- [ ] Sample README: migration, store, guard, shared backend, schema ownership and the production runbook for the
      migration job.
- [ ] The README is the only place the state pattern is documented; framework docs stay feature-only.

### Deliverables

- `Trivium.Sample.Persistence` with its schema, migration, store, guard and Redis-compatible backend.
- Aspire AppHost wiring, tests and the sample README.

### Exit criteria

- [ ] The sample migrates an empty database and is idempotent.
- [ ] A shared backend implements the same contract and passes the M5 contract tests, including lease takeover.
- [ ] The store passes CAS, scoping and cancellation tests.
- [ ] The bot runs with neither PostgreSQL nor Redis configured.
- [ ] No project under `src` references a database driver, a migration tool or a schema.

### Risks / watchouts

- Sample code rots because it is not a package. Mitigation: the sample is built, migrated and tested in CI on every
  change.
- Copy-paste upgrades. Mitigation: the sample is a starting point that the application owns; no semver promise on its
  schema.
- The blog/docs treat the sample as a framework feature. Mitigation: the README says it is application code, and the
  framework reference page covers dedup only.

---

## M7 — Complete Telegram coverage

**Goal:** every update type exposed by Telegram.Bot 22.10.3.3 has a first-class routing path and typed context, and
Bot API 8–10 features are supported at the routing/typed-context/helper level.

**Depends on:** M1 (can run in parallel with M2–M6). **Scope size:** XL, recurring.

**Non-goals:** no high-level payment/gift/story state machines; no wrapper for every Bot API method.

### Workstreams

#### 7.1 Update types

- [ ] Implement and test all 28 `UpdateKind` values:
      `BusinessConnection`, `BusinessMessage`, `CallbackQuery`, `ChannelPost`, `ChatBoost`, `ChatJoinRequest`,
      `ChatMember`, `ChosenInlineResult`, `DeletedBusinessMessages`, `EditedBusinessMessage`,
      `EditedChannelPost`, `EditedMessage`, `GuestMessage`, `InlineQuery`, `ManagedBot`, `Message`,
      `MessageReaction`, `MessageReactionCount`, `MyChatMember`, `Poll`, `PollAnswer`, `PreCheckoutQuery`,
      `PurchasedPaidMedia`, `RemovedChatBoost`, `ShippingQuery`, `StoppedMessageGeneration`, `Subscription`,
      `Unknown`.
- [ ] Convenience attributes for each (see [Telegram coverage](../reference/telegram-coverage.md)).
- [ ] Typed context accessors for each; no `!` null-forgiving in user-facing code.
- [ ] Actor/chat resolution for each (M1 matrix).
- [ ] `AllowedUpdates` generation includes exactly the routed update kinds, per bot.
- [ ] `Unknown` updates are logged and routed to an optional catch-all; they never throw or crash the loop.
- [ ] One test per update kind that builds a realistic payload and asserts the handler ran with the right context.

#### 7.2 Bot API 8–10 features

Support at routing/typed/helper level (details and status in the
[Bot API 8–10 feature matrix](../reference/telegram-coverage.md#Bot%20API%208%E2%80%9310%20feature%20matrix)):

- [ ] **Business accounts**: connection, business messages, edited/deleted business messages, read/delete, profile,
      gifts, stars, stories.
- [ ] **Reactions**: `message_reaction`, `message_reaction_count`, set/delete reactions.
- [ ] **Boosts**: `chat_boost`, `removed_chat_boost`, `getUserChatBoosts`.
- [ ] **Paid media**: `purchased_paid_media`, `sendPaidMedia`, live photos, media groups.
- [ ] **Stars/payments**: balance, transactions, refunds, subscription edits, invoice/pre-checkout/shipping.
- [ ] **Gifts**: send gift, premium subscription gift, available gifts, user/chat/business gifts, convert/upgrade/
      transfer.
- [ ] **Stories**: post/repost/edit/delete for managed business accounts.
- [ ] **Checklists**: send/edit, tasks done/added service messages, reply to task.
- [ ] **Rich messages**: send/edit/draft, `SendHtml`, rich blocks, rich media.
- [ ] **Ephemeral messages**: send/edit/delete, receiver user, reply parameters, callback replacement.
- [ ] **Guest mode**: `guest_message`, `AnswerGuestQuery`, guest caller metadata.
- [ ] **Managed bots**: `managed_bot` update, get/replace token, access settings, keyboard request.
- [ ] **Communities**: service messages and chat fields.
- [ ] **Suggested posts**: approve/decline, direct-message topics, prices, service messages.
- [ ] **Live photos**: send, paid media, albums.
- [ ] **Polls 9.x/10.x**: media, multiple correct answers, options added/deleted, descriptions, country codes,
      members-only, persistent IDs, revoting/shuffle.
- [ ] **Verification**: verify/remove user/chat.
- [ ] **Profile**: set/remove bot photo, user profile audios, emoji status.
- [ ] **Member tags**: set tag, tag fields, owner left/changed.
- [ ] **Message drafts**: `sendMessageDraft`, `sendRichMessageDraft`, `stopped_message_generation`.
- [ ] **Webhook management**: secret, allowed updates, max connections, certificate/IP, info.
- [ ] **Mini App helpers**: `initData` validation, `answerWebAppQuery`, prepared messages/keyboard buttons.

#### 7.3 Helpers (not wrappers)

- [ ] `ctx.ReplyAsync`, `ctx.SendAsync`, `ctx.EditAsync`, `ctx.DeleteAsync`, `ctx.AnswerCallbackAsync`,
      `ctx.AnswerInlineAsync`.
- [ ] `SendHtml`/`EditHtml` convenience helpers built on Telegram.Bot 22.7+; no custom HTML parser.
- [ ] `DownloadFileAsync` helper with size limits and cancellation.
- [ ] `MessageLink`, `IsServiceMessage`, `ChatMember.IsInChat/IsAdmin` exposed where useful.
- [ ] `ChatId`/`UserId` conversions are explicit and test-covered.
- [ ] Helpers never swallow Telegram API exceptions; they surface `ApiRequestException` with context.
- [ ] No helper duplicates a Telegram.Bot extension with a different name.

#### 7.4 Command catalog and localization

- [ ] Generated catalog includes command, description, scope, language, hidden flag.
- [ ] `SetMyCommands` sync supports per-language and per-scope commands.
- [ ] Command descriptions can come from resources; missing translations fall back.
- [ ] Localized command list documented as a first-class feature.
- [ ] Command text normalization (mentions, whitespace) is consistent with routing.

#### 7.5 Coverage tests

- [ ] `UpdateBuilder` fixtures for every update kind, with realistic optional fields.
- [ ] Golden tests: serialized Telegram JSON → deserialized `Update` → dispatched handler → captured response.
- [ ] Matrix test asserts every `UpdateType` is either routed or explicitly documented as `Unknown`/deferred.
- [ ] Bot API feature matrix is generated or checked against a hand-maintained table; CI fails on drift.
- [ ] Sample `Trivium.Sample.Business` demonstrates business messages + stars/gifts/stories at routing level.

### Deliverables

- Complete update-kind coverage and fixtures.
- Feature matrix reference kept current with Telegram.Bot releases.
- Business sample.
- Localized command catalog.

### Exit criteria

- [ ] Every `UpdateType` in Telegram.Bot 22.10.3.3 has a test and a documented route attribute.
- [ ] No update kind throws `NullReferenceException`, `KeyNotFoundException` or `Single()` from the dispatcher.
- [ ] `AllowedUpdates` for a sample bot contains only the kinds it routes.
- [ ] Bot API 8–10 matrix has no `Not started` rows for routing/typed context; helpers are listed where applicable.
- [ ] Localized command sync is tested for at least two languages and two scopes.
- [ ] The business sample runs end-to-end against a fake client and is documented.

### Risks / watchouts

- Telegram ships Bot API changes every few months (10.3 was August 2026). Mitigation: a coverage matrix, a dependency
  update cadence, and a "new update kind" checklist.
- Scope creep into full payment/gift frameworks. Mitigation: the support-level taxonomy explicitly stops at helpers and
  samples.
- Fixtures rot with Telegram.Bot upgrades. Mitigation: JSON-based fixtures, not hand-built object graphs where possible.

---

## M8 — Observability, resilience and security

**Goal:** production operators can see what happened, bound the blast radius, and trust the endpoint under load and
attack.

**Depends on:** M3, M4. **Scope size:** L.

### Workstreams

#### 8.1 Telemetry

- [ ] `ActivitySource "Trivium"` and `Meter "Trivium"` in core.
- [ ] One activity per update: tags `trivium.bot_id`, `trivium.bot_name`, `trivium.update_id`,
      `trivium.update_kind`, `trivium.route`, `trivium.handler`, `trivium.transport`, `trivium.outcome`.
- [ ] Spans for data access, webhook parsing, dedup and Telegram API calls (via an instrumented `ITelegramBotClient`
      decorator, or `TelegramBotClient` request events where available).
- [ ] Metrics: updates received/processed/failed, dispatch duration histogram, handler duration, Telegram request
      duration/status, dedup hits/misses, queue depth, application data access duration.
- [ ] `Trivium.OpenTelemetry` wires meters/activities and provides `WithTriviumInstrumentation()`.
- [ ] Structured logs with scopes; `LoggerMessage` source generation.
- [ ] Redaction: never log message text, captions, tokens, callback payloads, file URLs or phone numbers at Information
      level. Debug logging requires explicit opt-in and still redacts secrets.
- [ ] Tests: activity tags, metric emission, redaction.

#### 8.2 Resilience

- [ ] Telegram.Bot built-in retry (`RetryCount`, `RetryThreshold`) configured per bot; document 429 behavior.
- [ ] Optional per-bot outbound rate limiter (`System.Threading.RateLimiting`) with token bucket, not a sleeping
      semaphore.
- [ ] Broadcast mode: explicit API for sending to many chats with pacing, cancellation and progress.
- [ ] Polling backoff with jitter; circuit breaker for persistent failures (401/403/409).
- [ ] Webhook backpressure and timeout policies (M4).
- [ ] Data-access failure policy: retry only idempotent reads; writes rely on unique constraints/upserts.
- [ ] Graceful degradation: an application can configure a data-access outage to fail updates or degrade a feature.
- [ ] Document scaling model: vertical first; horizontal needs shared dedup and idempotent handlers; the outbox is
      deferred beyond 1.0.

#### 8.3 Security

- [ ] Webhook secret validation and rotation (M4).
- [ ] Optional IP allowlist documented; not a substitute for the secret.
- [ ] Request size limits and JSON depth limits.
- [ ] Callback data is untrusted input: validate version, length and shape; never deserialize arbitrary types.
- [ ] Token handling: environment/user-secrets only; never in logs, telemetry, exceptions or `/health`.
- [ ] Mini App `initData` validation helper with constant-time HMAC and expiry check.
- [ ] SQL injection: application SQL is parameterized; the framework issues no SQL of its own.
- [ ] PII retention policy: what is stored, why, for how long, how to delete.
- [ ] Dependency and container scanning in CI.
- [ ] Apply the [security checklist](../fundamental/quality.md#Security%20checklist).

### Deliverables

- `Trivium.OpenTelemetry` package.
- Metrics/traces/logs reference and a sample dashboard (Grafana JSON or documented queries).
- Resilience configuration reference.
- Security checklist and PII policy.

### Exit criteria

- [ ] An update can be traced end-to-end (webhook/polling → route → handler → data access → Telegram call) by `UpdateId`.
- [ ] Metrics include the documented names and labels; cardinality is bounded.
- [ ] No secret or message text appears in logs/telemetry in the redaction tests.
- [ ] Security checklist is signed off; no high/critical dependency findings.

### Risks / watchouts

- Telemetry cardinality explosion (chat ids as labels). Mitigation: bounded labels only; ids go in structured logs, not
  metric labels.
- Resilience hides real failures. Mitigation: explicit policies, observable retries, no silent drop.
- Security is easy to defer. Mitigation: M8 is a hard gate before the release candidate.

---

## M9 — Samples, templates and documentation

**Goal:** a new user can go from zero to a running, testable bot in minutes; an operator can run it in production; a
contributor can understand every design decision.

**Depends on:** M2–M8. **Scope size:** L.

### Workstreams

#### 9.1 Samples

- [ ] `Trivium.Sample.Polling` — console host, attributes, groups, guards, arguments.
- [ ] `Trivium.Sample.Webhook` — ASP.NET Core minimal API, secret, ingress verdict, health.
- [ ] `Trivium.Sample.MultiBot` — two bots, per-bot routes and commands.
- [ ] `Trivium.Sample.Business` — business messages, reactions, stars/gifts/stories routing.
- [ ] `Trivium.Sample.Persistence` — application-owned schema, Dapper store, state guard, Redis-compatible dedup backend.
- [ ] Each sample has a README, `.env.example`/user-secrets instructions, and a fake-client test.
- [ ] Samples are built and tested in CI; no hardcoded tokens or connection strings.

#### 9.2 Templates

- [ ] `Trivium.Templates` with `dotnet new trivium-bot`.
- [ ] Options: `--transport polling|webhook`, `--persistence none|postgres`.
- [ ] The `postgres` option scaffolds a sample migration and store into the project so the application owns them.
- [ ] Template includes Dockerfile, appsettings, health endpoint, `.editorconfig`, and a test project.
- [ ] Template smoke test in CI: `dotnet new`, restore, build, test, run with fake token.
- [ ] Template version matches package version.

#### 9.3 Documentation

- [ ] `docs/fundamental/`: architecture, routing, handlers, hosting, observability, testing, quality, security, AOT.
- [ ] `docs/reference/`: packages, update types, feature matrix, diagnostics, deduplication, public API.
- [ ] `docs/plan/`: the roadmap and milestones in this document.
- [ ] Quickstart: polling and webhook, each under 10 minutes.
- [ ] Cookbook: callback keyboards, pagination, stateful forms, media, payments, business bots, localization,
      multi-bot, testing.
- [ ] Operations: configuration reference, webhook setup/rotation, migrations, backups, retention, scaling, health,
      dashboards.
- [ ] XML docs on all public APIs; DocFX API reference; VitePress guide site.
- [ ] Docs CI: build, link check, markdown lint, code snippet compilation where feasible.

### Deliverables

- Five runnable samples.
- `dotnet new` template.
- Published docs site.
- Quickstarts and operations runbook.

### Exit criteria

- [ ] A clean machine can run the quickstart in under 10 minutes, including a real bot token.
- [ ] Every sample builds and its tests pass in CI.
- [ ] `dotnet new trivium-bot` produces a project that builds and passes its generated test.
- [ ] Docs site has zero broken links and zero warnings.
- [ ] Every public type/member has XML docs.
- [ ] Operations guide covers webhook secret rotation, migration rollback/backup, and retention.
- [ ] No sample or doc contains a real token, secret or production connection string.

### Risks / watchouts

- Docs drift. Mitigation: docs are part of the definition of done; feature matrix is checked in CI.
- Samples rot with API changes. Mitigation: samples are part of the build and test matrix from M9 onward.
- Too many samples. Mitigation: five focused samples; cookbook covers variants.

---

## M10 — Release 1.0

**Goal:** freeze the public API, validate the complete product, publish signed packages and a supported documentation
set.

**Depends on:** all previous milestones. **Scope size:** M.

### Workstreams

#### 10.1 API freeze and compatibility

- [ ] Public API snapshot committed; changes require an explicit review.
- [ ] `Microsoft.DotNet.ApiCompat` (or equivalent) validates package-to-package compatibility.
- [ ] SemVer policy documented: no breaking changes in minor/patch after the plan; deprecation policy with one minor
      release of notice.
- [ ] Obsolete APIs have `[Obsolete]` with a migration message and an analyzer warning.
- [ ] Internal types are `internal`; public types are intentional; no accidental surface from generator output.
- [ ] Nullable annotations audited.

#### 10.2 Packaging and publishing

- [ ] Package signing (NuGet author/repository signing as available); symbols and SourceLink verified.
- [ ] NuGet trusted publishing from GitHub Actions with a protected environment and manual approval.
- [ ] `README.md` per package; icon; license; repository metadata; release notes.
- [ ] Versioning by git tag; the `v1.0.0` tag produces `1.0.0` packages. No public preview releases.
- [ ] Package validation: install into a clean template, build, run; uninstall cleanly.
- [ ] Dependency version policy documented (minimum Telegram.Bot, supported .NET).

#### 10.3 Final validation

- [ ] Full test matrix: unit, integration, generator, analyzer, architecture, AOT trim analysis, package validation.
- [ ] Security review: webhook, secrets, callback data, application SQL, PII, dependency scan.
- [ ] Docs site published; quickstart re-tested from a clean machine.
- [ ] No open P0/P1 issues; P2 issues triaged with owners.
- [ ] Support matrix: .NET 10, Telegram.Bot 22.10.x, Linux x64/arm64; the persistence sample targets PostgreSQL 18 and
      Redis-compatible servers.
- [ ] `SECURITY.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, issue/PR templates, release process.

#### 10.4 Release notes

- [ ] Architecture overview and package map.
- [ ] Quickstart links.
- [ ] Known limitations: full Native AOT deferred, no outbox, no distributed queue, no EF adapter.
- [ ] Link to the "Explicitly deferred" section.

### Exit criteria

- [ ] `1.0.0` packages published to nuget.org and install cleanly in a fresh template.
- [ ] CI is green on the release commit; all gates enabled.
- [ ] Public API is frozen and documented.
- [ ] Security checklist signed off.
- [ ] Docs site and quickstarts work from a clean machine.
- [ ] No known P0/P1 defects.
- [ ] Support matrix and limitations are explicit.

---

## Explicitly deferred

Ordered by expected value, not commitment. These capabilities are outside this plan; they are recorded so scope stays
honest.

| Item                                      | Why it is deferred                                                                                                      | Notes                                                                                        |
| ----------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| **Native AOT validation + sample**        | Design is AOT-ready, but full validation needs real publish/run matrices                                                | `PublishAot`, trimming, single-file, Linux arm64; Telegram.Bot AOT support is the dependency |
| **Durable outbox / queue**                | The plan uses dedup + `AfterProcess`; an outbox is application infrastructure with its own operational docs             | Postgres outbox + background worker; at-least-once                                           |
| **Horizontal scale beyond shared dedup**  | The shared dedup backend is sample code, not a product; scale also needs idempotent handlers and a documented model     | The sample's Redis backend supports multi-instance; document the scaling model               |
| **First-party stores or adapters**        | Persistence is application code; a first-party store would need schema ownership and a support matrix                   | Dapper/Npgsql, EF Core or any other tool, written by the application                         |
| **Runtime route registration**            | Routes come from attributes at compile time; routes from configuration or a database cannot be validated before startup | A table-composition API after 1.0, if a real need appears                                    |
| **Conversation DSL**                      | The sample's store is enough; a DSL needs design                                                                        | Builder/state-machine layer on top of the application's own store                            |
| **First-party RBAC / policy engine**      | Applications differ; guards keep authorization application-defined                                                      | Custom `IUpdateGuard` implementations, middleware and the app's own principal store          |
| **Audit log addon**                       | A framework-owned audit table is a policy decision; apps own their audit trail                                          | App store or external log pipeline                                                           |
| **i18n framework**                        | Command localization is in scope; message localization is application-level                                             | Resource-based `IStringLocalizer` integration                                                |
| **Admin/diagnostics UI**                  | Not core; could be a separate app                                                                                       | Read-only route/command/health viewer                                                        |
| **High-level Stars/gifts/payments flows** | The plan is routing + helpers by design                                                                                 | Idempotency, receipts, refunds, reconciliation                                               |
| **Mini App validation helpers**           | Basic `initData` validation is in scope                                                                                 | Extended WebApp helpers, prepared messages                                                   |

---

## Risk register

| Risk                                       | Likelihood | Impact   | Mitigation                                                                                     |
| ------------------------------------------ | ---------- | -------- | ---------------------------------------------------------------------------------------------- |
| Bot API scope explosion                    | High       | High     | Support-level taxonomy; routing/typed/helper, not full frameworks; matrix-driven milestones    |
| Source generator complexity / IDE slowdown | Medium     | High     | Incremental generator, no retained symbols, snapshot + perf tests, contract-tested route table |
| Multi-bot route scoping confusion          | Medium     | Medium   | Explicit `[Bot]`, startup route inventory, tests for every combination                         |
| Sample rot                                 | Medium     | Medium   | The persistence sample is built, migrated and tested in CI on every change                     |
| Webhook "exactly once" expectations        | High       | High     | Document at-least-once, dedup, idempotent handlers, outbox backlog                             |
| AOT incompatibility in dependencies        | Medium     | Medium   | Trim analysis from day one, AOT-ready design, full validation after the plan                   |
| Public API freeze too early                | Medium     | High     | API snapshot, design review, internal dogfooding before 1.0                                    |
| Docs drift                                 | High       | Medium   | Docs in definition of done, matrix checks, samples in CI                                       |
| Dedup backends diverge                     | Medium     | Medium   | Shared contract tests; the in-memory implementation is the reference semantics                 |
| Security/PII incident                      | Low        | Critical | Secret validation, redaction tests, security checklist, dependency scanning                    |

---

## Milestone review checklist

Use this at the end of every milestone:

- [ ] Exit criteria met and evidenced (test names, docs links).
- [ ] Global definition of done satisfied.
- [ ] Public API reviewed for naming, nullability, cancellation and XML docs.
- [ ] Docs updated (fundamental, reference, plan).
- [ ] Locked decisions reviewed; any change is recorded in this document.
- [ ] Risk register reviewed; new risks added.
- [ ] Packages build and install-test from a local feed.
- [ ] Retro notes: what was harder than expected, what to change in the next milestone.
