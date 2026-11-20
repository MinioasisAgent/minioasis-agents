- **Access:** Level 1
- **Last Updated:** 2026-11-20

# State

A snapshot of where the work stands. Agents overwrite this file in the pull request that changes it; it is not a log. Read it first in every session.

This file holds only what GitHub and Git cannot show. Work in progress is the list of open pull requests, and the maintainer's queue is the open pull requests labelled `needs-human`.

## Current work

- None.

## What exists

- **The rulebook,** and the starting configuration: `.editorconfig`, `.gitattributes`, `.gitignore`, the official Koha contract (`contracts/official_koha_openapi.json`), and the pinned NSwag client generator (`dotnet-tools.json`, `nswag.json`).
- **No application code, no test, and no spec.** Everything is written from the start, under these rules.
- The technical baseline is decided in ADR 0003. The solution and the projects are not built yet.

## Next

- The foundation, which is the first row of `docs/ROADMAP.md`. No C# check can run before it exists.
- Then the first spec. The maintainer chooses it; the roadmap proposes an order.

## Recent decisions

- ADR 0001: two-repository workflow with autonomous agents.
- ADR 0002: development process adapted from GSD Core and brave-core.
- ADR 0003: technical baseline.
- ADR 0004: models per role.

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
