---
title: Design & Roadmap
last_reviewed: 2026-11-21
---

# Trivium — Design & Roadmap

[← Back to AGENTS.md](../AGENTS.md)

**Related:** [Roadmap](plan/roadmap.md) · [Architecture](fundamental/architecture.md) ·
[Deduplication](reference/deduplication.md) · [Quality gates](fundamental/quality.md) ·
[Telegram coverage](reference/telegram-coverage.md) · [Diagnostics](reference/diagnostics.md)

> **Status:** M0 (foundation) complete; M1 (kernel) in progress. There is no public package; the first and only release
> is 1.0, published after every milestone in the [roadmap](plan/roadmap.md) is complete.

> **Internal document.** This page, `fundamental/` and `plan/` are contributor and agent documentation. The VitePress
> site publishes only the public landing page and, as features ship, `guide/` and `reference/` pages.

The published site is a GitHub Pages project page at <https://vodemsharp.github.io/trivium/>, built from `docs/` with
`base: "/trivium/"` and deployed by `.github/workflows/deploy-docs.yml` on a push to `main` that touches the docs build
inputs. The `Docs` workflow stays the PR gate; the deploy workflow only runs on `main` and `workflow_dispatch`.

## Where to start

| If you are…                            | Read                                                                                    |
| -------------------------------------- | --------------------------------------------------------------------------------------- |
| adding a handler, route or update kind | [Architecture](fundamental/architecture.md) → [Diagnostics](reference/diagnostics.md)   |
| sharing dedup or writing a filter      | [Deduplication](reference/deduplication.md)                                             |
| changing analyzers, build gates or CI  | [Quality gates](fundamental/quality.md)                                                 |
| planning scope or checking a milestone | [Roadmap & milestones](plan/roadmap.md)                                                 |
| checking Telegram update coverage      | [Telegram coverage](reference/telegram-coverage.md)                                     |
| changing these docs                    | [AGENTS.md](../AGENTS.md) → [Documentation system](../AGENTS.md#Documentation%20system) |

## What Trivium is

Trivium is a routing, dispatch and hosting framework for Telegram bots on .NET 10. It sits on top of
[Telegram.Bot](https://github.com/TelegramBots/Telegram.Bot) and answers one question well:

> Given one Telegram update, which handler should run, with which services, and what happens when it fails?

Telegram.Bot remains the Bot API client. Trivium owns the application layer around it: route tables, update contexts,
guards, middleware, multi-bot lifecycle, polling/webhook hosting, and optional update deduplication.

Trivium is **not**:

- a replacement for `Telegram.Bot` or a wrapper around every Bot API method;
- an ORM, an admin panel, a payment engine or a gift marketplace;
- a "magic" framework that discovers handlers with runtime reflection.

## Design principles

1. **Telegram.Bot does the API; Trivium does the application.** Every Bot API method stays available through
   `ITelegramBotClient`. Trivium adds routing and lifecycle, not a second API surface.
2. **No reflection in the hot path.** Attributes, routes, invokers, callback-data codecs and DI registrations are
   generated at compile time. Runtime dispatch is direct calls and immutable lookup tables.
3. **Fail at build or startup, never per update.** Duplicate routes, ambiguous prefixes, invalid handler signatures and
   callback payloads over 64 bytes are diagnostics or startup errors.
4. **One update, one route.** Multiple matches are a configuration error, not a runtime `Single()` exception. Fan-out
   is not supported; compose handlers explicitly when one update has multiple concerns.
5. **The core stores nothing.** `Trivium` has no notion of users, state or schema. Persistence is application code:
   schema, stores, migrations and conversation state all belong to the bot. Authorization is application-defined too:
   Trivium ships guards, not roles.
6. **Multi-bot is first-class.** Per-bot clients, options, transports, route scopes and data partitions — without
   global mutable dictionaries.
7. **Polling and webhooks are equal citizens.** A console worker and an ASP.NET Core host are both supported from day
   one, with separate packages so polling hosts do not drag in ASP.NET Core.
8. **Deterministic routing.** Exact match > longest parameterized prefix > default. The rules are documented, tested and
   independent of registration order.
9. **No swallowed errors, no `async void`.** Every update has a scope, a cancellation token, an error path and a log
   scope.
10. **AOT/trimming-safe by design.** No `MethodInfo.Invoke`, no `Activator.CreateInstance`, no runtime
    `MakeGenericType`, no `dynamic`, no Newtonsoft.Json. Full Native AOT validation is deferred beyond 1.0.
11. **No data layer.** The framework ships no schema, stores, migrations or state API; applications own persistence and
    use the tooling they already have. Dedup is ingress coordination, not storage, and it ships as its own package.
12. **Boring where it matters.** Explicit types, explicit ordering, explicit failures, immutable startup state.

## Package map

All `Trivium.*` package IDs are currently free on nuget.org.

| Package                 | Responsibility                                                                                                                                               | Depends on                                |
| ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------- |
| `Trivium`               | Core abstractions: `TriviumContext`, update kinds, route model, dispatcher, ingress filters, guards, middleware, attributes, argument parsing, reply helpers | `Telegram.Bot`                            |
| `Trivium.Deduplication` | `IUpdateDeduplicator`, dedup filter, in-memory implementation, `UseDeduplication()`                                                                          | `Trivium.Hosting`                         |
| `Trivium.Hosting`       | DI, options, bot registry, polling workers, command sync, health checks, graceful shutdown                                                                   | `Trivium`, `Microsoft.Extensions.Hosting` |
| `Trivium.AspNetCore`    | Webhook endpoint mapping, secret-token validation, JSON source-gen, request limits                                                                           | `Trivium.Hosting`, ASP.NET Core           |
| `Trivium.Generators`    | Incremental source generator + compile-time diagnostics (routes, invokers, DI, callback data)                                                                | Roslyn                                    |
| `Trivium.OpenTelemetry` | ActivitySource/Meter registration, OTel instrumentation helpers                                                                                              | `Trivium`                                 |
| `Trivium.Testing`       | Fake Telegram client, update builders, test host and clock fakes                                                                                             | `Trivium`                                 |
| `Trivium.Templates`     | `dotnet new trivium-bot` templates (polling, webhook, multi-bot)                                                                                             | —                                         |

`Trivium` knows the ingress seam (`IUpdateIngressFilter`, `IngressOutcome`) but no dedup policy.
`Trivium.Deduplication` supplies the filter and the in-memory deduplicator, and `UseDeduplication()` registers them, so
one registration makes deduplication run on every transport — polling, webhooks and in-process dispatch.
`Trivium.AspNetCore` never references it and maps a generic `Defer` outcome to `409 Conflict`. A shared backend is
application code implementing the same contract.

Applications in the repository (not packaged):

| Project                   | Purpose                                                          |
| ------------------------- | ---------------------------------------------------------------- |
| `samples/Trivium.AppHost` | Aspire AppHost: the persistence sample with PostgreSQL and Redis |

The samples set includes `Trivium.Sample.Persistence`, the reference for application-owned schema, stores, migrations
and a shared dedup backend, which lands with M6.

## Repository layout

```text
src/
  Trivium/                         core kernel
  Trivium.Deduplication/           dedup filter + in-memory default
  Trivium.Hosting/                 polling + DI + multi-bot
  Trivium.AspNetCore/              webhook host
  Trivium.Generators/              source generator
  Trivium.OpenTelemetry/           observability
  Trivium.Testing/                 test harness
  Trivium.Templates/               dotnet new templates
samples/
  Trivium.AppHost/                 Aspire orchestration for the samples
  Trivium.Sample.Polling/
  Trivium.Sample.Webhook/
  Trivium.Sample.MultiBot/
  Trivium.Sample.Business/
  Trivium.Sample.Persistence/      M6: schema, stores, migrations, dedup backend
tests/
  Trivium.Tests/                   unit + architecture
  Trivium.IntegrationTests/        webhook + Aspire + host lifecycle
  Trivium.Generators.Tests/        generator snapshots + analyzer tests
docs/
  fundamental/                      how we build
  reference/                        what exists / support matrices
  plan/                             roadmap and milestones
```

The layout follows the repository conventions (`.slnx`, central package management, warnings as errors, `src` +
`tests`), adapted for a framework rather than a product. Database and migration packages appear only in samples.

## Documentation map

| Doc                                                 | Contents                                                                             |
| --------------------------------------------------- | ------------------------------------------------------------------------------------ |
| [AGENTS.md](../AGENTS.md)                           | Working agreement, build gates, conventions, documentation system                    |
| [Roadmap](plan/roadmap.md)                          | **Main deliverable.** Locked decisions, milestones M0–M10, exit criteria, risks      |
| [Architecture](fundamental/architecture.md)         | Runtime pipeline, handler model, routing rules, source generator, multi-bot, webhook |
| [Deduplication](reference/deduplication.md)         | Ingress filters, dedup contract, in-memory default, shared backend                   |
| [Quality gates](fundamental/quality.md)             | Strict build, analyzers, tests, CI, packaging, release checklist                     |
| [Telegram coverage](reference/telegram-coverage.md) | All 28 update types, Bot API 8–10 feature matrix, helper coverage                    |
| [Diagnostics](reference/diagnostics.md)             | Compile-time routing diagnostics, house-style analyzers, startup checks              |

## Milestone summary

No dates and no mid-plan releases. Each milestone is an outcome with hard exit criteria; 1.0 is published only when
all of them pass.

| Milestone             | Outcome                                                                               |
| --------------------- | ------------------------------------------------------------------------------------- |
| **M0** Foundation     | Buildable, testable, analyzable framework skeleton with CI, AGENTS.md and docs        |
| **M1** Kernel         | Deterministic routing and dispatch with zero reflection; fake-update tests            |
| **M2** Generator      | Attribute-first handlers compile to a generated route table, invokers and diagnostics |
| **M3** Hosting        | DI, multi-bot registry, polling workers, command sync, health, graceful shutdown      |
| **M4** Webhook        | Secure ASP.NET Core webhook endpoint with the ingress verdict, limits and management  |
| **M5** Deduplication  | `Trivium.Deduplication`: contract, filter, in-memory default, contract tests          |
| **M6** Persistence    | The sample's own schema, store, guard and shared dedup backend                        |
| **M7** Coverage       | Every Telegram.Bot 22.10 update type routed and typed; Bot API 8–10 features          |
| **M8** Observability  | OTel traces/metrics/logs, resilience, load and security tests                         |
| **M9** Samples & docs | Samples, `dotnet new` templates, XML docs, published docs site                        |
| **M10** 1.0           | API frozen, packages signed and published, all gates green                            |

## Delivery model

Work lands in reviewable slices on feature branches (`chore/…`, `feat/…`, `docs/…`) and merges to `main` through pull
requests. Nothing is committed or pushed without explicit approval. There are no preview packages: the repository and
its samples are the development surface until 1.0.

## What "done" means

The project is complete when all of the following are true simultaneously:

- `dotnet build Trivium.slnx` is warning-free and `dotnet format --verify-no-changes` is clean.
- Unit, integration, generator, analyzer and architecture tests pass in CI.
- Every `UpdateType` exposed by Telegram.Bot 22.10.3.3 has a routing path and a test.
- No reflection, `dynamic`, Newtonsoft.Json or `async void` exists in a dispatch path (IL scan enforced).
- All public APIs have XML documentation and a public-API snapshot.
- The persistence sample applies its migration from an empty database, is idempotent, and is covered by Aspire
  integration tests.
- Webhook endpoints reject missing/invalid secrets, and retried updates are deduplicated through the shared ingress
  filter.
- Docs site builds, links resolve, and the quickstart produces a running bot from `dotnet new`.
- No open P0/P1 defects; the security checklist is signed off.
