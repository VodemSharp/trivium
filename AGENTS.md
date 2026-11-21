# Trivium — Agent Guide

This is the working agreement for anyone (human or agent) changing this repository. Read it before the first edit.

> **Git is approval-gated — the single most important rule.** Never `commit`, `amend`, `push`, `push --force`, `tag`,
> rewrite history, or open/update/close/merge a pull request without an explicit, per-action "go" from the user.
> Approval for one change never authorizes the next. Do the work, run the gates, present the diff, and **stop**.

## What Trivium is

Trivium is a routing, dispatch and hosting framework for Telegram bots on .NET 10. It sits on top of
[Telegram.Bot](https://github.com/TelegramBots/Telegram.Bot) and owns the application layer: route tables, update
contexts, guards, middleware, multi-bot lifecycle, polling/webhook hosting and optional update deduplication.

It is **not** a Bot API wrapper, an ORM, an admin panel, or a payment engine. Telegram.Bot remains the client;
every Bot API method stays available through `ITelegramBotClient`.

**Status:** M0 (foundation). There is no public release; 1.0 ships after every milestone in the
[roadmap](docs/plan/roadmap.md) is complete. There are no preview/version slices.

## Package map

| Package                 | Responsibility                                                                              |
| ----------------------- | ------------------------------------------------------------------------------------------- |
| `Trivium`               | Contexts, update kinds, routes, dispatcher, ingress filters, guards, middleware, attributes |
| `Trivium.Deduplication` | `IUpdateDeduplicator`, dedup filter, in-memory implementation, `UseDeduplication()`         |
| `Trivium.Hosting`       | DI, options, bot registry, polling, command sync, health, shutdown                          |
| `Trivium.AspNetCore`    | Webhook endpoint, secret validation, request limits                                         |
| `Trivium.Generators`    | Incremental source generator + compile-time diagnostics                                     |
| `Trivium.OpenTelemetry` | Activities, meters, instrumentation helpers                                                 |
| `Trivium.Testing`       | Fake client, update builders, test host, fakes                                              |
| `Trivium.Templates`     | `dotnet new trivium-bot` templates                                                          |

Dependency direction is enforced by architecture tests and must not be broken:

- `Trivium` references only `Telegram.Bot` — no hosting, ASP.NET Core, telemetry, database packages or dedup policy.
- `Trivium.Hosting` references `Trivium` + `Microsoft.Extensions.*`.
- `Trivium.Deduplication` references `Trivium.Hosting`; no server client and no database driver.
- `Trivium.AspNetCore` references `Trivium.Hosting` and ASP.NET Core; never `Trivium.Deduplication`.
- `Trivium.Generators` references Roslyn only; no `Microsoft.CodeAnalysis.Workspaces`.
- No project under `src` references Npgsql, Dapper, a database provider or a migration tool.
  Persistence is application code; the samples show how to write it.

## Locked design decisions

Changing any of these requires explicit review and a docs update in the same change. The authoritative list is
[locked decisions](docs/plan/roadmap.md#Locked%20decisions).

- .NET 10 only.
- `Telegram.Bot 22.10.3.3` / Bot API 10.3 baseline.
- Attribute-first handlers generated at compile time; attributes are the only route-authoring model.
- Zero runtime reflection in routing, dispatch, callback data and DI registration.
- Closed handler signature set; anything else is a diagnostic.
- `TriviumRouteTable` is the compilation target of the generator and the table type `Trivium.Testing` populates; it is
  data, not a second way to author routes.
- Host configuration stays fluent (`AddBot`, `UsePolling`, `AddGroup(...).UseMiddleware<T>()`).
- Core stores nothing: no schema, no stores, no migrations, no state API. Persistence is application code.
- Authorization is application-defined: Trivium ships guards, not roles or permissions.
- Ingress filters are core (`IUpdateIngressFilter`, `IngressOutcome`); dedup policy is not. It ships in
  `Trivium.Deduplication` with an in-memory default, and a shared backend is application code implementing the contract.
- No ORM, no data-access abstraction, no EF migrations in the framework.
- Multi-bot is first-class.
- Deterministic routing: update kind → exact → longest parameterized prefix → default.
- A failed guard stops the route; it never falls back to another route.
- Webhook secret validation; no durable outbox in this plan.
- AOT/trimming-safe by design; full Native AOT validation is deferred beyond 1.0.
- `TimeProvider` + `DateTimeOffset` for all time.

## Repository layout

```text
src/
  Trivium/                  core kernel
  Trivium.Deduplication/    dedup filter, in-memory default
  Trivium.Hosting/          polling, DI, multi-bot
  Trivium.AspNetCore/       webhook host
  Trivium.Generators/       source generator
  Trivium.OpenTelemetry/    instrumentation (M8)
  Trivium.Testing/          test harness
  Trivium.Templates/        dotnet new templates
  analyzers/                 in-house Roslyn analyzers + code fixes
samples/                     five runnable samples + Aspire AppHost (M9); Persistence owns its schema and stores (M6)
  Trivium.AppHost/           container orchestration for the samples
tests/                       unit, integration, generators, analyzers
scripts/
  docs/                      link, anchor and freshness tooling
docs/
  fundamental/               how we build
  reference/                 what exists / support matrices
  plan/                      roadmap + milestones
```

## Build and gates

```bash
dotnet restore Trivium.slnx
dotnet build Trivium.slnx -c Release
dotnet test Trivium.slnx
dotnet format Trivium.slnx --verify-no-changes
dotnet pack Trivium.slnx -c Release -o artifacts

bun install
bun run docs:test
bun run docs:check
bun run docs:build
bun run format:md:check
```

- `TreatWarningsAsErrors=true`: a warning is a failed build.
- `AnalysisMode=Recommended` plus the in-house analyzers. Every rule is `none` or `warning`; there is no advisory tier.
- Tests run on `xunit.v3` + Microsoft.Testing.Platform only. No VSTest, no `Microsoft.NET.Test.Sdk`, no Coverlet.
- Integration tests need Docker (Aspire AppHost); they are a pre-merge gate, not an edit-loop gate.
- `docs:check` fails on a dead relative link or a heading anchor that matches no heading; `docs:freshness` is advisory.
- Do not disable a gate, suppress a routing diagnostic, or merge with `--admin` to get green.

## C# conventions

The full list is in [Quality gates](docs/fundamental/quality.md).

- Name every `CancellationToken` parameter and local `ct` (`TRIVSTYLE0001`). Intentionally unused tokens may be `_`.
- Hoist awaited calls into locals — never inline `await` in arguments or conditions (`TRIVSTYLE0002`, `TRIVSTYLE0005`).
- Put object initializers on their own line (`TRIVSTYLE0003`).
- Put a blank line after a multi-line statement (`TRIVSTYLE0004`).
- Block bodies for methods, lambdas and local functions; expression bodies only for properties.
- One public type per file, named after the type; file-scoped namespaces; no tuples in signatures.
- Generated types are singular and describe the artifact they own (`TriviumRouteTable`, `TriviumServiceMap`,
  `TriviumCommandCatalog`), never plural container classes (`TriviumRoutes`, `TriviumServices`, `TriviumCommands`).
- Value objects for identifiers (`BotId`, `UserId`, `ChatId`, `MessageId`, `UpdateId`).
- Public API is `sealed` unless inheritance is a designed extension point; XML docs are mandatory on public API.
- No `DateTime.Now/UtcNow`, `Task.Result`, `Thread.Sleep`, `new HttpClient()`, `async void`, Newtonsoft.Json, or
  reflection in a dispatch path. `BannedSymbols.txt` and the analyzers enforce most of this.
- Line length 120; LF; UTF-8.

## Implementation rules

- **One update, one route.** Multiple matches are a configuration error; there is no fan-out. Compose handlers explicitly
  when one update has multiple concerns.
- **Fail at build or startup.** Duplicate routes, ambiguous prefixes, invalid handler signatures, missing secrets and
  invalid tokens never become per-update failures.
- **No guard fallback.** A denied route does not execute and does not fall through.
- **Dedup is an ingress filter, not a transport feature.** Polling, webhooks and in-process dispatch share
  `IUpdateIngressFilter` and its `IngressOutcome`; `Trivium.Deduplication` supplies the filter and the in-memory
  deduplicator, and `UseDeduplication()` enables them for every transport. The polling offset remains the primary
  acknowledgement.
- **Scopes per update.** Scoped services live and die with the update; no ambient static bot state.
- **Cancellation everywhere.** Transport, host shutdown and timeouts flow into the handler token. The handler token does
  not carry the webhook request's abort token: a transport timeout must not cancel work whose reply goes out through
  `ITelegramBotClient`.
- **No swallowed exceptions.** Classify them; `OperationCanceledException` is not an error.
- **No data layer in the framework.** Schema, migrations, stores and conversation state are application code; the
  persistence sample is the reference.
- **Generated code is not public API.** Only attributes and runtime types are contracts.

## Documentation system

The docs are a cross-linked "AI database" in three tiers:

- **Fundamental** — hand-curated _principles and conventions_: this file plus the `docs/fundamental/*.md` guides
  ([architecture](docs/fundamental/architecture.md), [quality](docs/fundamental/quality.md)). Edit these by hand when a
  convention actually changes. They describe _how we build_, not _what currently exists_.
- **Reference** — `docs/reference/*.md` ([telegram coverage](docs/reference/telegram-coverage.md),
  [diagnostics](docs/reference/diagnostics.md), [deduplication](docs/reference/deduplication.md)): what exists and what
  is supported right now.
- **Plan** — [`docs/plan/roadmap.md`](docs/plan/roadmap.md): locked decisions, milestones M0–M10 and exit criteria.

[`docs/index.md`](docs/index.md) is the public landing page.
[`docs/README.md`](docs/README.md) is the internal design summary, guide map and maintainer entry point.

**Published vs internal:** the VitePress site publishes only user-facing pages — `docs/index.md` today, plus
`docs/guide/**` and as-built `docs/reference/**` as features ship. `docs/README.md`, `docs/fundamental/**` and
`docs/plan/**` are contributor and agent documentation; `srcExclude` keeps them out of the site build. A reference page
is published only when its behavior exists; forward-looking design stays internal. To publish a page, remove its path
from `srcExclude` and add it to the nav or sidebar.

**Cross-link convention:** internal guides under `docs/` open with `[← Back to AGENTS.md](../../AGENTS.md)` and a
`**Related:** …` line. Published pages link only to other published pages or GitHub; they never link to internal files or
repo-root documents, because the site build cannot resolve them.

**Linking rules** (enforced by `bun run docs:check` in CI): links are relative markdown with the `.md` extension —
never wikilinks (`[[Page]]`) and never a directory, because GitHub, VitePress and Obsidian all resolve the same
relative path. **Anchors are surface-dependent.** In the vault — this file and everything under `docs/` — an anchor
uses the heading's literal text, `%20`-encoded (`#Locked%20design%20decisions`), because Obsidian matches heading
text, not GitHub's slugified ids; GitHub and VitePress therefore land at the top of the target file rather than the
section, which is why every link's text names the section it points at. The GitHub-only files (`README.md` and
anything under a dotfolder such as `.github/`, which Obsidian never shows) use the slug instead (`#build`) so they
jump on GitHub. A heading that any doc links to must stay free of inline code and brackets, whose encoded form does
not reliably match.

**Frontmatter:** every guide under `docs/` carries `title`, `last_reviewed` (an ISO date) and — only where the mapping
is tight — `code_globs`, the repo-relative paths whose changes would invalidate the guide. `bun run docs:freshness`
dates each guide against its globs' last commit and prints a table; it runs weekly and on doc PRs and is warning-only,
because a glob match is a prompt to re-read a guide, not proof it is wrong. Both commands run on the same parser
(`scripts/docs/markdown.ts`), which has unit tests (`bun run docs:test`).

**Keeping docs current:** when a change adds or alters routing, hosting, persistence, diagnostics or Telegram coverage,
update the matching fundamental/reference/plan doc in the same change. There is no separate regeneration step;
keeping the docs in sync is the agent's job. Bump `last_reviewed` when you actually re-check a guide against the code,
not merely when you touch its prose.

**Editing locally in Obsidian** (optional, and nothing about it is committed): open the **repository root** as the
vault — `AGENTS.md` and `README.md` must stay there, and neither a copy nor a symlink into `docs/` works (agent
discovery expects the root file, git records a symlink as a symlink, and the docs site would build a second page).
Obsidian hides dotfolders (`.git`, `.github`, `.idea`) on its own but lists build output and code trees such as
`bin/`, `obj/`, `node_modules/`, `artifacts/`, `TestResults/`, `src/`, `samples/`, `tests/` and
`docs/.vitepress/cache` — add those to _Settings → Files and links → Excluded files_, which keeps them (and the
`AnalyzerReleases.*.md` notes under `src/`) out of search, the graph and backlinks even though the file pane still
shows them. Turn **off** "Use Wikilinks", set the new-link format to the relative path and enable automatic link
updates — backlinks, the graph and rename-refactors then work off the same relative links GitHub and VitePress read.

Other documentation rules:

- Every public type/member has XML docs. `CS1591` is an error.
- The docs site is VitePress (`docs/.vitepress/config.mts`) and publishes only the user-facing pages listed above; API
  reference with DocFX lands in M9.
- Do not add versioned release notes or preview plans: there is one release.

## Working agreement

1. Read `docs/plan/roadmap.md` and the relevant fundamental doc before editing.
2. Create a feature branch (`chore/…`, `feat/…`, `fix/…`, `docs/…`). Never work directly on `main`.
3. Make the smallest coherent change. Update tests and docs in the same slice.
4. Run `dotnet build`, `dotnet format --verify-no-changes`, and the relevant tests.
5. Present the diff and the gate results. **Stop.** Do not commit, push, tag or open a PR without explicit approval.
6. After approval, commit with a Conventional Commit message; one logical change per PR.

## Definition of done

A change is done when behavior is tested, public API is documented and snapshotted, no banned symbols or new warnings
exist, format is clean, cancellation/`TimeProvider` are used where relevant, docs are updated, the security checklist
is applied when surface changed, and CI is green. For a milestone, every exit criterion in the roadmap must pass.
