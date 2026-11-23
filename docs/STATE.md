- **Access:** Level 1
- **Last Updated:** 2026-11-23

# State

A snapshot of where the work stands. Agents overwrite this file in the pull request that changes it; it is not a log. Read it first in every session.

This file holds only what GitHub and Git cannot show. Work in progress is the list of open pull requests, and the maintainer's queue is the open pull requests labelled `needs-human`.

## Current work

- Brief 001, get item (`docs/briefs/001-get-item.md`), is a draft that waits for the maintainer's acceptance. It also covers the Koha connection and the API error contract.

## What exists

- **The rulebook,** and the starting configuration: `.editorconfig`, `.gitattributes`, `.gitignore`, the official Koha contract (`contracts/official_koha_openapi.json`), which the maintainer supplied on 2026-11-23 as the contract of Koha 26.05, and the pinned NSwag client generator (`dotnet-tools.json`, `nswag.json`).
- **The foundation:** `minioasis.slnx` and the three projects of `AR-001` (`Minioasis.Application`, `Minioasis.Api`, `Minioasis.Koha`), and a `.Tests` project for each (`TS-004`), with the test framework xUnit v3. `scripts/preflight.sh` runs on them. How to build, test, and run is in `docs/running.md`.
- **No behavior and no test.** `Minioasis.Api` starts an empty host with no endpoints. Everything else is written from the start, under these rules.

## Next

- Build brief 001 once it is approved. Then brief 002, staff access control (`docs/ROADMAP.md`).

## Recent decisions

- The test framework, xUnit v3 (`xunit.v3.mtp-off`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`), accepted by merging the foundation pull request.

## Open questions for the maintainer

- **The one-time setup** in `docs/transfer.md`, section 2: is the `needs-human` label created, is `GoldBeluga` a collaborator, and is branch protection set?
- **Whether get item is transferred to production before staff access control (brief 002) is built.**

## Production transfers

| Capability | Transfer branch | Merged into production | Manual acceptance |
| --- | --- | --- | --- |
| — | — | none yet | — |

## Environment

- Agents need the .NET 10 SDK to run the checks.
- The project uses Koha 26.05 (`CLAUDE.md`, section 1). Manual acceptance runs against that version. The contract file does not state its Koha version: its `info.version` is the REST API version, `1`.
- Agents may call only the test Koha that the maintainer provides (`TS-013`). It runs Koha 26.05, as the maintainer states; no response shows the version. It sits behind Cloudflare Access, so every request sends the two Cloudflare Access headers (`TS-013`). A session reached it on 2026-11-23: a client-credentials token request with HTTP Basic, and one with the credentials in the body, returned 200; an API call with the token returned 200. It holds one hand-made record: bibliographic record 1 with item 1, barcode `MINIOASIS-TEST-0001`, added on 2026-11-23 at the maintainer's word. No check depends on it.
