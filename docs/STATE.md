- **Access:** Level 1
- **Last Updated:** 2026-11-20

# State

A snapshot of where the work stands. Agents overwrite this file in the pull request that changes it; it is not a log. Read it first in every session.

This file holds only what GitHub and Git cannot show. Work in progress is the list of open pull requests, and the maintainer's queue is the open pull requests labelled `needs-human`.

## Current work

- Spec 001, API errors and Koha connection: `approved` on 2026-11-20 (approving review by the maintainer on pull request #2). Its plan is `approved` (2026-11-20, under the maintainer's exception for spec 001 recorded in `plan.md`). Being built in the five pull requests the plan lists: pull request 1 (the Application error model) merged as #5; pull request 2 (the Api error contract) merged as #6; pull request 3 (Koha configuration and the generated OAuth client) is on the branch `ai/sprint/001-koha-config`.

## What exists

- **The rulebook,** and the starting configuration: `.editorconfig`, `.gitattributes`, `.gitignore`, the official Koha contract (`contracts/official_koha_openapi.json`), and the pinned NSwag client generator (`dotnet-tools.json`, `nswag.json`).
- **The foundation:** `minioasis.slnx` and the six projects that ADR 0003 decides (`Minioasis.Application`, `Minioasis.Api`, `Minioasis.Koha`, and a `.Tests` project for each), with the test framework xUnit v3. `scripts/preflight.sh` runs on them. How to build, test, and run is in `docs/running.md`.
- **The Application error model** of spec 001: error categories, definitions, the registry and its checks, results, `DependencyFailureOutcome`, `AddApplication()`, and their tests.
- **The Api error contract** of spec 001: every error response has the Problem Details shape with `code` and `traceId`; server failures are masked and logged once with the trace identifier; ASP.NET's own 404, 405, 400, 413, and 415 are shaped; `errors.en.json` is embedded and checked against the definitions at startup; endpoints come from `IEndpointModule` registrations. The shared Api test host (`ApiTestHostFactory`) exists, with dummy Koha settings. No production endpoint yet: every request to the running API is a 404 `not_found`. Everything else is written from the start, under these rules.
- **The Koha configuration** of spec 001: `AddKohaIntegration` reads and checks `MINIOASIS_KOHA_BASE_URL`, `_CLIENT_ID`, `_CLIENT_SECRET`, and the optional `_TIMEOUT_SECONDS` when the application starts, and refuses to start on a bad value, naming the variable (`https` required outside `Development`). It registers the options and the two named clients, `OAuth` and `Api`, without handlers yet. Nothing contacts Koha. The focused contract `contracts/oauth.openapi.json` and its generated client (`Minioasis.Koha/Generated/OAuth`) exist; nothing uses the client yet. The variables and the generation command are in `docs/running.md`.

## Next

- Spec 001, plan pull request 4: the token provider (R15 to R18, the token part of R23).

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
