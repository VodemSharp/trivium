---
title: Quality gates
last_reviewed: 2026-11-21
code_globs:
  - .editorconfig
  - BannedSymbols.txt
  - Directory.Build.props
  - Directory.Build.targets
  - Directory.Packages.props
  - .github/workflows/**
  - src/analyzers/**
  - tests/**
---

# Quality gates

[← Back to AGENTS.md](../../AGENTS.md)

**Related:** [Docs index](../README.md) · [Roadmap](../plan/roadmap.md) · [Architecture](architecture.md) ·
[Deduplication](../reference/deduplication.md) · [Diagnostics](../reference/diagnostics.md)

Trivium uses a strict build and review culture designed for a public library. The goal is binary: a rule is either off
or enforced; there is no advisory middle tier that everyone learns to ignore.

## Build system

| Concern      | Choice                                                                             |
| ------------ | ---------------------------------------------------------------------------------- |
| Solution     | `Trivium.slnx` (XML solution, no `.sln`)                                           |
| SDK          | .NET 10 (`global.json`, `rollForward: latestFeature`)                              |
| Target       | `net10.0` only                                                                     |
| Packages     | Central package management in `Directory.Packages.props`                           |
| Shared props | `Directory.Build.props`, `Directory.Build.targets`                                 |
| Warnings     | `TreatWarningsAsErrors=true`                                                       |
| Analysis     | `AnalysisMode=Recommended`, `EnforceCodeStyleInBuild=true`                         |
| Nullability  | `Nullable=enable` everywhere                                                       |
| Docs         | `GenerateDocumentationFile=true`; XML docs required on public API                  |
| AOT          | `IsAotCompatible=true`, `IsTrimmable=true`, `EnableTrimAnalyzer=true` on libraries |
| Formatting   | `dotnet format` + `.editorconfig`; LF; UTF-8                                       |
| Tests        | xunit.v3 on Microsoft.Testing.Platform                                             |
| Containers   | Aspire AppHost (`Aspire.Hosting.Testing`)                                          |
| CI           | GitHub Actions                                                                     |

### `Directory.Build.props` (shape)

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisMode>Recommended</AnalysisMode>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <IsAotCompatible>true</IsAotCompatible>
    <IsTrimmable>true</IsTrimmable>
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <Deterministic>true</Deterministic>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="AsyncFixer" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" PrivateAssets="all" />
    <PackageReference Include="Microsoft.VisualStudio.Threading.Analyzers" PrivateAssets="all" />
    <PackageReference Include="Roslynator.Analyzers" PrivateAssets="all" />
    <PackageReference Include="Roslynator.CodeFixes" PrivateAssets="all" />
    <PackageReference Include="Roslynator.Formatting.Analyzers" PrivateAssets="all" />
    <AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.txt" />
  </ItemGroup>
</Project>
```

### Commands

```bash
dotnet build Trivium.slnx -c Release
dotnet test Trivium.slnx
dotnet format Trivium.slnx --verify-no-changes
dotnet format analyzers Trivium.slnx --no-restore --severity info
dotnet pack Trivium.slnx -c Release -o artifacts
```

Integration tests use MTP filtering:

```bash
dotnet test Trivium.slnx -- -class "*WebhookTests"
dotnet test Trivium.slnx -- -method "*DuplicateUpdate*"
```

## Analyzer stack

| Source             | Role                                                               |
| ------------------ | ------------------------------------------------------------------ |
| Built-in `CA*`     | Recommended rules at warning; selected extras promoted             |
| IDE code style     | Naming, `var`, braces, file-scoped namespaces, one type per file   |
| Roslynator `RCS*`  | Selected defensive/formatting rules, deny-by-default               |
| AsyncFixer         | Unnecessary async, unawaited calls, nested tasks, `Task<T>`→`Task` |
| VSTHRD             | Async/threading correctness (`100/103/107/110/114`)                |
| BannedApi `RS0030` | Time, blocking, `HttpClient`, Newtonsoft                           |
| In-house rules     | Shipped `TRIVIUM*` diagnostics; repository-only `TRIVSTYLE*` rules |

### Banned symbols

`BannedSymbols.txt` must ban at least:

- `DateTime.Now`, `DateTime.Today`, `DateTime.UtcNow`
- `DateTimeOffset.Now`, `DateTimeOffset.UtcNow`
- `Task.Wait`, `Task.WaitAll`, `Task.WaitAny`
- `Task<T>.Result`, `ValueTask<T>.Result`, `TaskAwaiter.GetResult`
- `Thread.Sleep`
- `new HttpClient()`
- Newtonsoft.Json entry points (`JsonConvert`, `JsonSerializer`)
- `MethodInfo.Invoke`, `Delegate.DynamicInvoke` (outside tests/generator)
- `Activator.CreateInstance` (outside tests/framework internals)
- `Console.WriteLine` in libraries (use `ILogger`)

### In-house analyzers

These are the repository's own rules, implemented in `src/analyzers`. They apply to this repository's code only and run
as part of its build. Consumer-facing diagnostics — `TRIVIUM0001`–`TRIVIUM0013` — come from
`Trivium.Generators` and are documented in the [diagnostics reference](../reference/diagnostics.md).

| ID              | Rule                                      |
| --------------- | ----------------------------------------- |
| `TRIVSTYLE0001` | `ct` naming for cancellation tokens       |
| `TRIVSTYLE0002` | Hoist awaited calls out of argument lists |
| `TRIVSTYLE0003` | Object initializer on its own line        |
| `TRIVSTYLE0004` | Blank line after a multi-line statement   |
| `TRIVSTYLE0005` | Hoist awaited calls out of conditions     |

Each analyzer rule is either `none` or `warning`; these five are on, so a violation fails the build. A new rule joins
the family together with its fixes.

## C# style conventions

The C# conventions are:

- **Cancellation tokens are named `ct`** — parameters and locals. Two or more tokens in one scope are exempt; `_` is
  allowed for intentionally unused tokens.
- **Hoist awaited calls** out of argument lists and conditions into locals.
- **Prefer block bodies** over expression-bodied methods, lambdas and local functions. Expression bodies are for
  properties.
- **One public type per file**, named after the type. Nested request/response types are allowed for samples/tests.
- **File-scoped namespaces**, `using` directives only where needed, no fully qualified names.
- **No tuples** in signatures or returns; declare a named record/struct.
- **No magic strings**; constants or value objects.
- **Value objects for identifiers** (`BotId`, `UserId`, `ChatId`, `MessageId`, `UpdateId`).
- **No XML doc comments on internal code**; XML docs are required on public API (library deviation).
- **No explanatory inline comments** unless the code cannot express the reason; docs carry conventions.
- **`var`** only when the type is obvious from the right-hand side.
- **Braces always**, even for one-line blocks.
- **Line length 120**, LF line endings, UTF-8.

### Library-specific style

- Public API is `sealed` unless inheritance is a designed extension point.
- Public types are documented; internal types are not part of the contract.
- Async methods return `Task`/`ValueTask`; no `async void`.
- Exceptions are for exceptional conditions; dispatch failures are classified, not thrown to the loop.
- No public mutable static state.
- No `#pragma warning disable` without a linked issue and a comment.
- No `NotImplementedException` in shipped code; use `NotSupportedException` with a clear message if a path is
  intentionally unsupported.

## Testing strategy

### Test projects

| Project                    | Scope                                                                               |
| -------------------------- | ----------------------------------------------------------------------------------- |
| `Trivium.Tests`            | Unit: routing, dispatch, guards, middleware, context, arguments, helpers, dedup     |
| `Trivium.Analyzers.Tests`  | In-house analyzer diagnostics and code fixes                                        |
| `Trivium.Generators.Tests` | Generator snapshots, incremental behavior, compile-time diagnostics                 |
| `Trivium.IntegrationTests` | Webhook + Aspire + host lifecycle; the persistence sample's store and dedup backend |

### Test architecture

- xunit.v3 on Microsoft.Testing.Platform only; no VSTest adapter, no `Microsoft.NET.Test.Sdk`, no Coverlet collector.
- Integration tests use the Aspire AppHost (`Aspire.Hosting.Testing`), one migrated template database, one cloned
  database per test.
- `Trivium.Testing` provides:
  - `FakeTelegramBotClient` capturing requests and returning configured responses;
  - `UpdateBuilder` for every `UpdateKind`, with realistic optional fields;
  - `TriviumTestHost` for in-process dispatch with DI and fake time;
  - `FakeTimeProvider` (from `Microsoft.Extensions.TimeProvider.Testing`);
  - the in-memory deduplicator from `Trivium.Deduplication` for deterministic ingress tests.
- Tests are deterministic: no `DateTime.Now`, no real network, no shared database state, no sleeps. Use `TimeProvider`
  and `TaskCompletionSource`/signals instead of delays.

### Coverage matrix

| Area              | Required tests                                                                                                |
| ----------------- | ------------------------------------------------------------------------------------------------------------- |
| Routing           | Exact, prefix, longest prefix, boundary, case, mention, caption, default, duplicate, ambiguous, no match      |
| Dispatch          | One route, no route, multiple matches rejected, guard deny, middleware order, cancellation, handler exception |
| Actor/chat        | Every update kind that has an actor/chat; null payload handling                                               |
| Generator         | Golden output, incremental cache, every diagnostic, fixes, 1,000-route perf                                   |
| Callback data     | Round-trip, Unicode, max size, version mismatch, malformed, unknown fields                                    |
| Hosting           | Start/stop, two bots, route scoping, command sync, health, config validation, polling dedup                   |
| Webhook           | Secret, dedup, duplicates, oversized, malformed, backpressure, queue-full release, timeout, shutdown          |
| Deduplication     | Contract tests shared by implementations; in-memory lease, expiry, abandon; webhook 200/409 mapping           |
| Persistence       | Sample store: CAS race, purge by `updated_at`, cancellation; Redis dedup: duplicates, lease takeover          |
| Telegram coverage | One test per update kind, golden JSON, drift check                                                            |
| AOT/trimming      | Trim analysis clean; reflection IL scan; generated code compiles                                              |
| Architecture      | Dependency direction; no forbidden references; public API snapshot                                            |

### Architecture tests

- `Trivium` references `Telegram.Bot` only; not `Trivium.Hosting`, `Trivium.AspNetCore`, `Trivium.Deduplication`,
  ASP.NET Core or a database driver.
- `Trivium.Hosting` references `Trivium` only among Trivium projects.
- `Trivium.Deduplication` references `Trivium.Hosting` only among Trivium projects; no server client.
- `Trivium.AspNetCore` references `Trivium.Hosting` only among Trivium projects, plus ASP.NET Core; never
  `Trivium.Deduplication`.
- No project under `src` references Npgsql, Dapper, a database provider or a migration tool.
- No `MethodInfo.Invoke`/`DynamicInvoke`/`Activator.CreateInstance` in dispatch IL.
- No `async void` in production assemblies (IL/metadata scan).
- Public API snapshot: `Microsoft.CodeAnalysis.PublicApiAnalyzers` with `PublicAPI.Shipped.txt` and
  `PublicAPI.Unshipped.txt` in every packable library. An undeclared public symbol is `RS0016` and fails the build, so a
  public API change always carries an intentional snapshot update.

## CI gates

Every pull request runs:

1. `dotnet restore Trivium.slnx`
2. `dotnet format Trivium.slnx --verify-no-changes`
3. `dotnet build -c Release` (warnings as errors; includes the public API snapshot analyzer)
4. ReSharper inspections (`jb inspectcode`, no issues above `HINT`)
5. `dotnet test` unit + generators + analyzers
6. `dotnet pack` into `artifacts/` (README, icon, XML docs, symbols)
7. Docs build + link check + markdown lint (`.github/workflows/docs.yml`)
8. CodeQL (`.github/workflows/codeql.yml`), dependency review on pull requests
   (`.github/workflows/dependency-review.yml`), secret scanning
9. Integration tests (Aspire AppHost, Linux) — from M6, when the AppHost and containers exist
10. Trim analysis (`PublishTrimmed` sample build) — once a host sample exists (M3 onwards)

Release tags additionally run:

- full test matrix (Linux x64/arm64 where practical);
- package signing;
- install/run smoke test from the `dotnet new` template;
- NuGet trusted publishing with a protected environment and manual approval.

A red gate is never bypassed by merging with `--admin` or disabling the workflow. Fix the gate or revert.

## Packaging

| Concern      | Rule                                                                |
| ------------ | ------------------------------------------------------------------- |
| Versioning   | Git tags; `v1.0.0` at completion; no public preview releases        |
| Symbols      | `.snupkg` + SourceLink                                              |
| XML docs     | Included in every package                                           |
| README       | Per-package README, license, icon                                   |
| Metadata     | Repository URL, tags, description, `PackageLicenseExpression=MIT`   |
| API compat   | `Microsoft.DotNet.ApiCompat` against the previous published package |
| Signing      | Repository/author signing as available; verify before publish       |
| Dependencies | Minimum `Telegram.Bot 22.10.3.3`; no floating ranges                |
| Target       | `net10.0` only                                                      |
| Trimming     | `IsTrimmable=true`; trim analysis clean                             |
| AOT          | Design-clean; full validation deferred beyond 1.0                   |

## Security checklist

Run for every change that adds network, persistence or auth surface:

- [ ] No secret is logged, traced, returned by health, or embedded in an exception.
- [ ] Webhook secret is validated with a constant-time comparison; production cannot start without it.
- [ ] Request body and JSON depth are limited.
- [ ] Callback data is treated as untrusted; version, size and shape are validated.
- [ ] Application SQL is parameterized; no dynamic concatenation.
- [ ] Guards are opt-in, but a denied route never falls through to another route.
- [ ] Per-bot isolation is enforced in every application store query.
- [ ] PII is minimized; retention is documented; redaction is tested.
- [ ] Rate limits/backpressure exist where an attacker can amplify work.
- [ ] Dependency and container scans are clean (no high/critical).
- [ ] Error responses do not reveal internals.
- [ ] TLS is required for webhooks and, where the application uses one, database connections in production.

## Documentation gates

- Every public type/member has XML docs (`CS1591` is an error for public API).
- `docs/fundamental` describes how to build; `docs/reference` describes what exists; `docs/plan` describes why/when.
- A change that alters routing, hosting, persistence or coverage updates the matching doc in the same PR.
- Docs build (VitePress + DocFX) succeeds; links resolve; markdown lint passes.
- Code snippets in docs are compiled where practical (samples referenced by path).

## Definition of done

A change is done when:

- [ ] Behavior is implemented with tests (unit/integration/generator as applicable).
- [ ] Public API has XML docs and a snapshot update if intentional.
- [ ] No banned symbols, no new warnings, format clean.
- [ ] Cancellation and `TimeProvider` are used where relevant.
- [ ] Docs updated (fundamental/reference/plan).
- [ ] Security checklist applied if surface changed.
- [ ] CI green; no gate disabled.
- [ ] For a milestone: all exit criteria in the [roadmap](../plan/roadmap.md) are met.

## Deliberate deviations

| Typical application convention  | Trivium                                                   | Reason                                                |
| ------------------------------- | --------------------------------------------------------- | ----------------------------------------------------- |
| No XML docs / `CS1591` silenced | XML docs required on public API                           | Library consumers need IntelliSense and API reference |
| `FluentResult` everywhere       | Handlers return `Task`/`ValueTask`; no forced result type | A library must not impose an application result model |
| No ORM; explicit SQL            | The framework ships no data layer at all                  | Persistence and schema belong to the application      |
| Application-owned analyzers     | `TRIVIUM*` analyzers                                      | Same philosophy, different IDs                        |
| Single product app              | Multi-package library + apps                              | Package boundaries are the architecture               |
