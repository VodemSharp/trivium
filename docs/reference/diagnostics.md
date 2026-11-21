---
title: Diagnostics reference
last_reviewed: 2026-11-16
code_globs:
  - src/Trivium.Generators/**
  - src/analyzers/**
---

# Diagnostics reference

[← Back to AGENTS.md](../../AGENTS.md)

**Related:** [Docs index](../README.md) · [Source generator](../fundamental/architecture.md#Source%20generator) ·
[Roadmap](../plan/roadmap.md) · [In-house analyzers](../fundamental/quality.md#In-house%20analyzers)

Trivium diagnostics fall into two groups:

- **Compile-time diagnostics** from `Trivium.Generators` (routing, signatures, callback data).
- **House-style analyzers** (`TRIVSTYLE0001`–`TRIVSTYLE0005`) that keep this repository's code consistent with its own
  conventions, as part of the repository build.

> **Status:** preliminary. This table is the planned diagnostic set; IDs and severities are confirmed as each rule
> lands in M2 and reviewed before M2 exits. Once a diagnostic ships, its ID is a stable public identifier, and
> removing or renaming it after 1.0 is a breaking change.

## Compile-time diagnostics

| ID            | Severity | Rule                                                                           | Typical fix                                                                                          |
| ------------- | -------- | ------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------- |
| `TRIVIUM0001` | Error    | Duplicate route: same update kind, exact key and bot scope                     | Remove one route or change its text/path/bot scope                                                   |
| `TRIVIUM0002` | Error    | Ambiguous parameterized prefixes: two routes tie for the longest match         | Make one prefix longer or merge the handlers                                                         |
| `TRIVIUM0003` | Error    | Multiple default/catch-all routes for the same kind and bot                    | Keep one default; use guards/state for branching                                                     |
| `TRIVIUM0004` | Error    | Unsupported handler return type                                                | Return `Task`, `Task<T>`, `ValueTask` or `ValueTask<T>`                                              |
| `TRIVIUM0005` | Error    | Unsupported handler parameter                                                  | Use `TriviumContext`, the typed payload, `CancellationToken`, `CommandArguments` or `[CallbackData]` |
| `TRIVIUM0006` | Error    | Invalid `[CallbackData]` type                                                  | Make it a `partial` record/class with supported members                                              |
| `TRIVIUM0007` | Warning  | Callback payload may exceed 64 bytes                                           | Remove fields, shorten values or use a raw path                                                      |
| `TRIVIUM0008` | Error    | Route attribute used on the wrong update kind                                  | Use the attribute matching the handler's payload                                                     |
| `TRIVIUM0009` | Error    | Static/instance handler mismatch                                               | Make the method static in a static class, or the class instance-based                                |
| `TRIVIUM0010` | Warning  | Route has no description and is not hidden                                     | Add `Description`, or mark the route `Hidden`                                                        |
| `TRIVIUM0011` | Error    | `[Bot]` name is empty or invalid                                               | Use a non-empty, valid bot name; unknown names fail at startup                                       |
| `TRIVIUM0012` | Error    | More than one `CancellationToken` parameter                                    | Remove the duplicate                                                                                 |
| `TRIVIUM0013` | Error    | Handler type is not accessible to generated code (private/nested/inaccessible) | Make the handler class and method `public` or `internal` and the containing type accessible          |

### Notes

- `TRIVIUM0001`–`TRIVIUM0003` are the routing invariants. They are **errors**, not warnings: an ambiguous route table
  must never ship.
- `TRIVIUM0007` is a warning because the generator cannot always know the encoded size at compile time (runtime values
  may be shorter or longer). The runtime encoder enforces the 64-byte limit regardless.
- `TRIVIUM0011` only validates the shape of the name. Whether the bot is declared in configuration is a **startup**
  concern (see below).
- `TRIVIUM0013` is resolved by making the handler (and its containing type) accessible to generated code. Generated
  code lives in the same assembly, so `internal` is sufficient unless the handler is nested in a private type.

## House-style analyzers (repository only)

| ID              | Severity | Rule                                                  | Typical fix                                      |
| --------------- | -------- | ----------------------------------------------------- | ------------------------------------------------ |
| `TRIVSTYLE0001` | Warning  | `CancellationToken` parameters/locals are named `ct`  | Rename; `_` is allowed when intentionally unused |
| `TRIVSTYLE0002` | Warning  | Awaited call is inlined as an argument or chained     | Assign it to a local first                       |
| `TRIVSTYLE0003` | Warning  | Object initializer must be on its own line            | Put `new Foo` and `{ ... }` on separate lines    |
| `TRIVSTYLE0004` | Warning  | Multi-line statement must be followed by a blank line | Insert a blank line                              |
| `TRIVSTYLE0005` | Warning  | Awaited call is inlined in a condition                | Assign it to a local, then branch on that local  |

These rules are the Trivium house-style family. All five are enabled as `warning` in `.editorconfig`, and
`TreatWarningsAsErrors` turns a violation into a build failure.

## Startup validation

These are not compiler diagnostics because they depend on runtime configuration:

| Check                                         | Behavior                                                  |
| --------------------------------------------- | --------------------------------------------------------- |
| Missing bot token                             | Startup error with the bot name, no token in the message  |
| Invalid/duplicate bot id                      | Startup error                                             |
| Unknown `[Bot]` name in a route               | Startup error listing the route                           |
| Missing webhook secret in Production          | Startup error                                             |
| Webhook URL not HTTPS in Production           | Startup error                                             |
| Duplicate bot name                            | Startup error                                             |
| No transport configured                       | Startup error unless the bot is explicitly disabled       |
| Data access configured but migrations missing | Health check failure; startup warning or error per policy |
| Command sync failure                          | Logged; does not stop the bot unless configured           |
| Unresolvable handler service                  | Startup error (DI validation)                             |

Startup validation is part of the host, not the core, and runs before any transport starts.

## Suppression policy

- Do not suppress a routing diagnostic. Fix the route.
- Do not suppress `TRIVIUM0007`; shorten the payload or use a raw path.
- A style suppression requires a comment with the reason and, for non-trivial cases, a linked issue.
- `#pragma warning disable` is not allowed in generated code; the generator must emit clean code.

## Adding a diagnostic

1. Reserve the next `TRIVIUM*` ID in this table.
2. Add the analyzer/generator rule and a unit test for the diagnostic and (if applicable) the code fix.
3. Add it to [in-house analyzers](../fundamental/quality.md#In-house%20analyzers) if it is a house-style rule.
4. Document the severity, message and fix here.
5. If it is a new routing invariant, add a regression test to the routing matrix.
