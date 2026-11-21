# Trivium

<p align="center"><img src="assets/badge.svg" alt="Trivium" width="96" height="96"></p>

Trivium is a routing, dispatch and hosting framework for Telegram bots — for when `/start` isn't enough.

> **Status:** M0 (foundation) in progress. There is no public package; 1.0 ships after every milestone in the
> [roadmap](docs/plan/roadmap.md) is complete.

## What it provides

- **One update, one handler.** Exact match, longest parameterized prefix, then the default; no fan-out or
  registration-order surprises.
- **Compiled, not reflected.** Attribute-first handlers become a route table, direct invokers and callback-data codecs;
  no reflection in the hot path.
- **Polling or webhooks.** First-class multi-bot hosting with per-bot routes, commands, transports and data partitions.
- **Your data stays yours.** No schema, stores or migrations in the core; dedup is an ingress filter and persistence is
  application code.
- **Production concerns included.** AOT/trimming-safe core, OpenTelemetry and a documented security model.
- **Coverage of current Telegram update types**, including business, reactions, boosts, paid media, guest mode, managed
  bots and subscriptions.

## Start here

| Doc                                                      | Contents                                                           |
| -------------------------------------------------------- | ------------------------------------------------------------------ |
| [AGENTS.md](AGENTS.md)                                   | Working agreement, build gates, conventions, dependency rules      |
| [Docs index](docs/README.md)                             | Design summary, principles, package map, milestone overview        |
| [Roadmap](docs/plan/roadmap.md)                          | Locked decisions and milestones M0–M10 with exit criteria          |
| [Architecture](docs/fundamental/architecture.md)         | Runtime pipeline, routing, generator, multi-bot, webhook           |
| [Deduplication](docs/reference/deduplication.md)         | Ingress filters, dedup contract, in-memory default, shared backend |
| [Quality gates](docs/fundamental/quality.md)             | Build, analyzers, tests, CI, packaging, security checklist         |
| [Telegram coverage](docs/reference/telegram-coverage.md) | Update-type matrix and Bot API 8–10 feature matrix                 |

## Build

```bash
dotnet restore Trivium.slnx
dotnet build Trivium.slnx -c Release
dotnet test Trivium.slnx
bun install && bun run docs:build
```

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md) and [AGENTS.md](AGENTS.md). Security issues go through
[SECURITY.md](SECURITY.md), never a public issue.

## License

MIT.
