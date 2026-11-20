- **Access:** Level 1
- **Last Updated:** 2026-11-20

# State

A snapshot of where the work stands. Agents overwrite this file in the pull request that changes it; it is not a log. Read it first in every session.

This file holds only what GitHub and Git cannot show. Work in progress is the list of open pull requests, and the maintainer's queue is the open pull requests labelled `needs-human`.

## Current work

- Spec 001, API errors and Koha connection: `approved` on 2026-11-20 (approving review by the maintainer on pull request #2). Next: the plan.

## What exists

- **The rulebook,** and the starting configuration: `.editorconfig`, `.gitattributes`, `.gitignore`, the official Koha contract (`contracts/official_koha_openapi.json`), and the pinned NSwag client generator (`dotnet-tools.json`, `nswag.json`).
- **The foundation:** `minioasis.slnx` and the six projects that ADR 0003 decides (`Minioasis.Application`, `Minioasis.Api`, `Minioasis.Koha`, and a `.Tests` project for each), with the test framework xUnit v3. `scripts/preflight.sh` runs on them. How to build, test, and run is in `docs/running.md`.
- **No behavior and no test.** Spec 001 is approved; its plan is next. `Minioasis.Api` starts an empty host with no endpoints. Everything else is written from the start, under these rules.

## Next

- The first spec. The maintainer chooses it; the roadmap proposes an order.

## Recent decisions

- ADR 0001: two-repository workflow with autonomous agents.
- ADR 0002: development process adapted from GSD Core and brave-core.
- ADR 0003: technical baseline.
- ADR 0004: models per role.
- The test framework, xUnit v3 (`xunit.v3.mtp-off`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`), accepted by merging the foundation pull request.

## Open questions for the maintainer

- **The Koha version.** Which Koha version is `contracts/official_koha_openapi.json` from? The file does not say: its `info.version` is the REST API version, `1`. Manual acceptance must run against that version.
- **The one-time setup** in `docs/workflow/production-transfer.md`: is the `needs-human` label created, is `GoldBeluga` a collaborator, and is branch protection set?
- **Which capability comes first,** and whether staff authentication must exist before the first capability is exposed.

## Production transfers

| Spec | Transfer branch | Merged into production | Manual acceptance |
| --- | --- | --- | --- |
| — | — | none yet | — |

## Environment

- Agents need the .NET 10 SDK to run the checks.
- Agents have no access to a live Koha.
