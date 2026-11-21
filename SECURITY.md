# Security policy

## Supported versions

Trivium has not published a release yet. The supported version after 1.0 is the latest 1.x release.

## Reporting a vulnerability

Do **not** open a public issue. Use GitHub's private vulnerability reporting for this repository
(**Security → Report a vulnerability**) or contact the maintainers privately.

Include:

- the affected package and version;
- a minimal reproduction or a clear description of the attack;
- the impact (for example, forged webhook updates, secret disclosure, SQL injection);
- any suggested mitigation.

You will get an acknowledgement as soon as possible, and a coordinated disclosure once a fix is available.

## Scope

In scope:

- webhook secret validation and transport-agnostic update deduplication;
- token/secret leakage through logs, telemetry, health endpoints or exceptions;
- callback-data validation and size enforcement;
- cross-bot key leakage in dedup keys, or SQL injection in an application store built from the sample;
- guard bypasses (a denied route must not fall through);
- dependency vulnerabilities reachable from the packages.

Out of scope:

- vulnerabilities in Telegram itself or in `Telegram.Bot` (report those upstream);
- bots built with Trivium that leak their own configuration or domain data;
- denial of service caused by a bot's own handler code.
