- **Access:** Level 1
- **Last Updated:** 2026-11-20

# Roadmap

Planned and completed work, in intended order. Agents may add `proposed` rows and update the status column. Only the maintainer's acceptance of a spec moves it to `approved` (see `docs/specs/README.md`). Ideas deferred from a spec are added under "Ideas not yet scheduled".

**Product scope** (from `CLAUDE.md`): staff circulation and catalogue workflows on top of Koha. Anything outside that scope needs the scope changed first, as a Level 2 change to `CLAUDE.md`.

The order below is a proposal. The maintainer decides it.

| # | Work | Status | Why / notes |
| --- | --- | --- | --- |
| — | Foundation | done | Not a spec. The solution, and the three projects with their test projects, as ADR 0003 describes, with xUnit v3 as the test framework. It added dependencies, so it was a Level 2 change. It added no behavior. |
| 001 | API errors and Koha connection | verified | `docs/specs/001-errors-and-koha-connection`. Combines the former items 001 (API error contract) and 002 (Koha connection) at the maintainer's request: the error contract (`ER-004` to `ER-007`, `ER-011`) and the shared Koha connection (`EX-004` to `EX-013`). |
| 002 | — | merged into 001 | Number not reused. |
| 003 | The first staff capability | to be decided | The maintainer chooses it. |

## Ideas not yet scheduled

The maintainer orders these. None is approved.

- **Staff authentication.** Until it exists, the API is open to anyone who can reach it.
- **A health endpoint,** for container and infrastructure monitoring.
- **Circulation and catalogue workflows,** for example item lookup, check out, check in, renew, holds, item search, and record edits.
- **Field-level validation errors,** a list of problems per field in a 400 response (deferred by spec 001, D3).
- **Mutation testing,** to measure how strong the tests are. It needs a new tool, so it is Level 2.
