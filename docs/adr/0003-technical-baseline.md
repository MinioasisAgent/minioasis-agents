- **Access:** Level 2
- **Last Updated:** 2026-11-20

# ADR 0003: Technical baseline

- **Status:** Accepted
- **Date:** 2026-11-19
- **Deciders:** Maintainer

## Context

The repository starts without application code. Agents write everything from the start, under the rulebook.

Some choices have to be fixed before the first line is written. The rules in `docs/best-practices/` need a concrete target, and several of these choices are one-way decisions that an agent must not make alone (see `CLAUDE.md`). This record states them in one place, so that the maintainer can see, and change, what the rulebook takes for granted.

## Decision

| # | Decision | Stated in the rules as |
| --- | --- | --- |
| 1 | The application is written in C# on .NET 10. | `CLAUDE.md`, section 3 |
| 2 | Three production projects at the repository root: `Minioasis.Application`, `Minioasis.Api`, and `Minioasis.Koha`. Each has one test project, `Minioasis.<Project>.Tests`. One solution file, `minioasis.slnx`. | `AR-001`, `AR-002`, `TS-004` |
| 3 | Koha is the only external system and the authority for library records. Minioasis has no storage of its own. | `CLAUDE.md`, section 3 |
| 4 | Koha is called through clients that NSwag generates from focused contracts. Each focused contract is derived from `contracts/official_koha_openapi.json`, which the maintainer supplies for the Koha version in use. Generated code lives in `Minioasis.Koha/Generated`. | `EX-001` to `EX-003` |
| 5 | Minioasis authenticates to Koha as a least-privilege service account, with OAuth 2 client credentials. | `EX-004` to `EX-009`, `EX-013` |
| 6 | Configuration is read from `MINIOASIS_KOHA_BASE_URL`, `MINIOASIS_KOHA_CLIENT_ID`, and `MINIOASIS_KOHA_CLIENT_SECRET`. | `EX-005` |
| 7 | The API has one error contract: Problem Details with `code` and `traceId`, a fixed status for each error category, and masked server failures. Client-visible messages are English and live in `Minioasis.Api/Resources/errors.en.json`. | `ER-003` to `ER-006`, `ER-011` |
| 8 | Formatting is defined in `.editorconfig` and checked with `dotnet format`. One script, `scripts/preflight.sh`, runs every check, and CI runs that script. | `CD-009`; `CLAUDE.md`, section 8 |

**Not decided here:**

- **Routes, request and response shapes, and public error codes.** Each is decided in the spec of its capability.
- **The test framework and every other package.** Each new dependency is a Level 2 change, accepted with the pull request that adds it.
- **Internal names and structure** beyond the rules in `docs/best-practices/`. These are two-way decisions that the agent makes.

## Consequences

- An agent starting from an empty repository knows what to create and where, without choosing a one-way decision itself.
- The rulebook names paths that do not exist yet: `minioasis.slnx`, `Minioasis.Koha/Generated`, `Minioasis.Api/Resources/errors.en.json`, and `docs/running.md`. The foundation work and the first specs create them. `.editorconfig` and `contracts/official_koha_openapi.json` came with the starting configuration.
- Koha operations are specified only against the official contract that the maintainer supplies.
- Every row above is the maintainer's choice. Any of them can be changed, cheaply, before code depends on it: change this record and the rules that state it, as one Level 2 change. After code depends on a row, changing it is a migration.
