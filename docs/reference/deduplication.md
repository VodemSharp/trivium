---
title: Deduplication
last_reviewed: 2026-11-21
code_globs:
  - src/Trivium/**
  - src/Trivium.Deduplication/**
  - src/Trivium.Hosting/**
  - samples/Trivium.Sample.Persistence/**
---

# Deduplication

[← Back to AGENTS.md](../../AGENTS.md)

**Related:** [Docs index](../README.md) · [Architecture](../fundamental/architecture.md) ·
[Roadmap](../plan/roadmap.md) · [Quality gates](../fundamental/quality.md)

Telegram delivers at least once. Deduplication is an ingress filter: it runs before routing, on every transport. It
keeps coordination state only — a bounded set of in-flight and recently handled update ids — and the framework still
owns no schema and no durable data.

## Where things live

| Concern                                  | Assembly                |
| ---------------------------------------- | ----------------------- |
| `IUpdateIngressFilter`, `IngressOutcome` | `Trivium`               |
| `IUpdateDeduplicator`, `DedupResult`     | `Trivium.Deduplication` |
| `UpdateDeduplicationFilter`              | `Trivium.Deduplication` |
| In-memory deduplicator and options       | `Trivium.Deduplication` |
| `UseDeduplication()`                     | `Trivium.Deduplication` |
| Shared backend                           | Your application        |

The seam is core because the dispatcher runs filters before routing. The policy is a package because a filter that keeps
a lease map, evicts entries and defines what "already handled" means is an implementation choice, not part of routing.

## Ingress filters

```csharp
public interface IUpdateIngressFilter
{
    ValueTask<IngressOutcome> FilterAsync(BotId botId, Update update, CancellationToken ct);
}

public enum IngressOutcome
{
    Continue,    // dispatch the update
    Acknowledge, // handled elsewhere: stop, and tell the transport to ack
    Defer,       // cannot run now: stop, and tell the transport to retry later
}
```

Filters run before the update context exists, so they take the bot, the raw update and the token — enough for
deduplication, allowlists and rate limiting without building a dispatch envelope.

Filters run in registration order; the first outcome other than `Continue` ends the pipeline. Transports translate it
into their own acknowledgement:

| Transport           | `Acknowledge`                 | `Defer`                           |
| ------------------- | ----------------------------- | --------------------------------- |
| Webhook             | `200 OK`                      | `409 Conflict`                    |
| Polling             | advance the offset            | stop the batch, back off, re-read |
| In-process dispatch | `DispatchResult.Acknowledged` | `DispatchResult.Deferred`         |

No transport references deduplication. `Defer` is generic: a rate limiter or a maintenance switch would use the same
outcome.

## Registering deduplication

```csharp
builder.Services.AddTrivium<TriviumRouteTable>(trivium =>
{
    trivium.AddBot("main", bot => bot.UsePolling());
    trivium.UseDeduplication(); // in-memory, single process
});
```

Deduplication is opt-in. Without it, delivery is at-least-once and handlers must be idempotent.

## Semantics

- `TryBeginAsync(bot, updateId, ct)` records an in-flight entry with a lease and returns `Accepted`, `Duplicate` or
  `InProgress`.
- `CompleteAsync` marks the entry done and keeps it for the dedup window.
- `AbandonAsync` removes it, so a retry after a rejected enqueue, cancellation or handler failure is not treated as a
  duplicate.
- An expired lease is reclaimable: a crash mid-handler must not block the retry forever.
- Entries are scoped by bot; update ids are not unique across bots.
- A backend outage degrades to duplicate deliveries, never to lost updates.

How a webhook answers an in-progress duplicate:

- Completed duplicate → `200 OK`; the update was already handled.
- In progress → `409 Conflict`; another delivery is running and the transport should retry later.
- The filter does not wait for the other delivery. Telegram's retry is the wait.
- The handler token carries host shutdown and the handler timeout, not the HTTP request's abort token. A transport
  timeout therefore does not cancel work whose reply is sent through `ITelegramBotClient`.
- Configure the handler timeout below Telegram's webhook timeout, so a healthy handler finishes before the first retry
  and `InProgress` stays a concurrency case rather than an everyday one.

Polling defers by not advancing its offset and never returns a status code.

## In-memory implementation

Correct for a single process: bounded, driven by `TimeProvider`, reset on restart. Two instances without a shared
backend will both process the same update, so a horizontally scaled deployment needs a shared backend.

## Shared backend

A shared backend is a small piece of application code implementing `IUpdateDeduplicator`. The sample implements one
over a Redis-compatible server in three commands:

```text
trivium:dedup:{bot_id}:{update_id}

begin     SET key "processing" NX PX <lease>    -> accepted
          on failure: GET key                    -> in-progress or duplicate
complete  SET key "completed" XX PX <window>
abandon   DEL key
```

- The lease exceeds the handler timeout, so a slow handler is not reclaimed while it runs.
- The window is at least Telegram's retry window; a week is a common choice.
- Single-key commands only, so the design works on a cluster.
- TLS and credentials come from the connection string and are never logged.
- A backend that cannot express an atomic `SET NX` is not suitable: `Get`-then-`Set` races.

## Contract tests

Every implementation passes the same tests:

- Concurrent duplicates: one `Accepted`, the rest `Duplicate` or `InProgress`.
- Crash between `TryBeginAsync` and `CompleteAsync`: the lease expires and the entry is reclaimable.
- `AbandonAsync` lets the next attempt be `Accepted`.
- Update ids are scoped per bot.
- Memory stays bounded under sustained traffic.

Conversation state is application code, not deduplication; `samples/Trivium.Sample.Persistence` owns that pattern.
