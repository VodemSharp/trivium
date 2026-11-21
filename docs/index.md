---
title: Overview
layout: home
last_reviewed: 2026-11-21

hero:
  name: Trivium
  text: When /start isn't enough.
  tagline: Routing, dispatch and hosting for Telegram bots. One update, one handler — compiled at build time.
  image:
    src: /logo.svg
    alt: Trivium
    width: 320
    height: 320
  actions:
    - theme: brand
      text: View on GitHub
      link: https://github.com/VodemSharp/trivium
    - theme: alt
      text: Roadmap
      link: https://github.com/VodemSharp/trivium/blob/main/docs/plan/roadmap.md

features:
  - title: One update, one handler
    details: Exact match, longest prefix, then the default. No registration-order surprises.
  - title: Compiled, not reflected
    details: Routes, invokers, DI and callback codecs are generated. No reflection in the hot path.
  - title: Your data stays yours
    details: No schema, no stores, no migrations. Dedup is an ingress filter; persistence stays in your code.
  - title: Many bots, one process
    details: Per-bot clients, transports, routes, commands and data partitions, isolated by design.
---

## After /start

Telegram bots start with `/start` and grow beyond a command handler. Trivium is the structure around that growth: one
update lands on exactly one handler, routing and callback codecs are generated at build time, and guards, middleware and
multi-bot hosting stay explicit.

## Status

::: info In development · M1
The design is settled and the implementation is underway. There is no public release yet; the
[roadmap](https://github.com/VodemSharp/trivium/blob/main/docs/plan/roadmap.md) tracks each milestone.
:::
