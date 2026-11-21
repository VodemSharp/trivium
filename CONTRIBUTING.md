# Contributing to Trivium

Thanks for helping. Trivium is a public .NET library with a deliberately strict bar: the build is warning-free, every
public API is documented, and routing invariants are tested.

## Before you start

- Read [AGENTS.md](AGENTS.md) and the [roadmap](docs/plan/roadmap.md).
- Check the milestone the change belongs to. Work outside the current milestone needs an issue first.
- For a design change, open a discussion/issue before writing code. The locked decisions are not casual preferences.

## Setup

Requirements:

- The .NET SDK pinned by `global.json`
- Docker (integration tests)
- Bun (docs site and markdown formatting)

```bash
dotnet restore Trivium.slnx
dotnet build Trivium.slnx -c Release
dotnet test Trivium.slnx
bun install
```

## Branches, commits and pull requests

- Branch from `main`: `feat/…`, `fix/…`, `chore/…`, `docs/…`.
- One logical change per pull request.
- Conventional Commits (`feat:`, `fix:`, `docs:`, `chore:`, `refactor:`, `test:`).
- A pull request must state the milestone/exit criterion it advances and the gates it ran.
- Do not commit secrets, tokens, production connection strings or generated `bin/`/`obj/` output.

## Gates

Every pull request must pass:

```bash
dotnet build Trivium.slnx -c Release
dotnet format Trivium.slnx --verify-no-changes
dotnet test Trivium.slnx
bun run docs:test
bun run docs:check
bun run format:md:check
bun run docs:build
```

Integration tests need Docker and run in CI. A red gate is fixed, not bypassed.

## Code and tests

- Follow the conventions in [AGENTS.md](AGENTS.md) and [Quality gates](docs/fundamental/quality.md).
- New behavior needs a test. Routing changes need table-driven cases; generator changes need snapshot/analyzer tests;
  persistence-sample changes need Aspire integration coverage.
- No runtime reflection in dispatch, routing, callback data or DI registration.
- No `async void`, no blocking on async work, no `DateTime.Now/UtcNow`; inject `TimeProvider`.
- Public API changes need XML docs and an intentional public-API snapshot update.

## Documentation

- Update the matching fundamental/reference/plan doc in the same pull request.
- Guides carry `title`, `last_reviewed` and — where the mapping is tight — `code_globs` frontmatter. Bump
  `last_reviewed` only when you actually re-check a guide against the code.
- Use relative markdown links with the `.md` extension (never wikilinks or directory links). Anchors in `AGENTS.md`
  and `docs/` use the heading's literal text, `%20`-encoded; GitHub-only files such as `README.md` use the slug.
  `bun run docs:check` verifies both.
- There are no versioned preview plans; the roadmap is the single source of truth.
- Do not include real bot tokens or secrets in docs or samples.

## Reporting security issues

See [SECURITY.md](SECURITY.md). Do not open a public issue for a vulnerability.
