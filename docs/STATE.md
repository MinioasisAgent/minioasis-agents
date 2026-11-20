- **Access:** Level 1
- **Last Updated:** 2026-11-20

# State

A snapshot of where the work stands. Agents overwrite this file in the pull request that changes it; it is not a log. Read it first in every session.

This file holds only what GitHub and Git cannot show. Work in progress is the list of open pull requests, and the maintainer's queue is the open pull requests labelled `needs-human`.

## Current work

- Spec 001, API errors and Koha connection: `approved` on 2026-11-20 (approving review by the maintainer on pull request #2). Its plan is `approved` (2026-11-20, under the maintainer's exception for spec 001 recorded in `plan.md`). Being built in the five pull requests the plan lists: pull request 1 (the Application error model) merged as #5; pull request 2 (the Api error contract) merged as #6; pull request 3 (Koha configuration and the generated OAuth client) merged as #7; pull request 4 (the token provider) merged as #8; pull request 5 (the handlers and the failure mapping) merged as #9; the verification fixes merged as #10. `verified` on 2026-11-20 at `fc6e4d9`; transfer branch `ready/001-errors-and-koha-connection` at the merge commit of the record. Open: pull request #4 (`HEAD` wording) and #11 (`IOException` wording in R20), both awaiting the maintainer.

## What exists

- **The rulebook,** and the starting configuration: `.editorconfig`, `.gitattributes`, `.gitignore`, the official Koha contract (`contracts/official_koha_openapi.json`), and the pinned NSwag client generator (`dotnet-tools.json`, `nswag.json`).
- **The foundation:** `minioasis.slnx` and the six projects that ADR 0003 decides (`Minioasis.Application`, `Minioasis.Api`, `Minioasis.Koha`, and a `.Tests` project for each), with the test framework xUnit v3. `scripts/preflight.sh` runs on them. How to build, test, and run is in `docs/running.md`.
- **The Application error model** of spec 001: error categories, definitions, the registry and its checks, results, `DependencyFailureOutcome`, `AddApplication()`, and their tests.
- **The Api error contract** of spec 001: every error response has the Problem Details shape with `code` and `traceId`; server failures are masked and logged once with the trace identifier; ASP.NET's own 404, 405, 400, 413, and 415 are shaped; `errors.en.json` is embedded and checked against the definitions at startup; endpoints come from `IEndpointModule` registrations. The shared Api test host (`ApiTestHostFactory`) exists, with dummy Koha settings. No production endpoint yet: every request to the running API is a 404 `not_found`. Everything else is written from the start, under these rules.
- **The Koha configuration** of spec 001: `AddKohaIntegration` reads and checks `MINIOASIS_KOHA_BASE_URL`, `_CLIENT_ID`, `_CLIENT_SECRET`, and the optional `_TIMEOUT_SECONDS` when the application starts, and refuses to start on a bad value, naming the variable (`https` required outside `Development`). It registers the options and the two named clients, `OAuth` and `Api`. Nothing contacts Koha at startup. The focused contract `contracts/oauth.openapi.json` and its generated client (`Minioasis.Koha/Generated/OAuth`) exist. The variables and the generation command are in `docs/running.md`.
- **The Koha token provider** of spec 001: `KohaAccessTokenProvider` requests a token with the client-credentials grant through the generated client on the `OAuth` client, checks the reply, caches it until 30 seconds before it expires, shares one request among callers that need a new token at the same moment (a caller's cancellation ends only its own wait; the request is cancelled when no caller waits), and discards a named token on `Invalidate`. Token-step failures are `KohaAuthenticationException` or `KohaUnavailableException`, with Koha's status and never its body. `AddKohaIntegration` registers it, and `TimeProvider.System` unless a time provider is registered. A caller whose token is already cancelled sends no token request.
- **The Koha handlers and failure mapping** of spec 001: `KohaDeadlineHandler` on both named clients puts one deadline (`MINIOASIS_KOHA_TIMEOUT_SECONDS`) on each send, until the body is read, and turns its expiry into `KohaUnavailableException`; on the `OAuth` client it bounds the shared token request on its own. `KohaBearerHandler`, inside it on the `Api` client, attaches the token, retries a `GET` once after a 401 with a new token, never sends another method again, and discards the token that got a 401 while it is still cached. Both clients use `SocketsHttpHandler` without automatic redirects and redact every logged header value. `KohaClientNames` documents the exceptions of a send. `KohaProtocolFailureMapper` translates a Koha status or a documented exception into `DependencyFailureOutcome` (R20). No capability calls Koha yet: the first one (spec 003) is the first user of the `Api` client and the mapper.

## Next

- Spec 001: the maintainer's manual acceptance (M1 to M3) and decisions on pull requests #4 and #11.

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
