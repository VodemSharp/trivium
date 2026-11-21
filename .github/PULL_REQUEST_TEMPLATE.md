## What changed

<!-- Explain the outcome, not the file list. -->

## Milestone

<!-- Which milestone/exit criterion does this advance? -->

## Checks

- [ ] `dotnet build Trivium.slnx -c Release` is warning-free.
- [ ] `dotnet format Trivium.slnx --verify-no-changes` is clean.
- [ ] `dotnet test Trivium.slnx` passes.
- [ ] Public API changes have XML docs and a snapshot update.
- [ ] Docs are updated in this change.
- [ ] `bun run docs:check`, `bun run format:md:check` and `bun run docs:build` pass when docs changed.
- [ ] No secrets, tokens or production connection strings are included.
