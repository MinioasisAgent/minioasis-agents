- **Access:** Level 1
- **Last Updated:** 2026-11-20

# Roadmap

Planned and completed work, in intended order. Agents may add `proposed` rows and update the status column. Only the maintainer's acceptance of a spec moves it to `approved` (see `docs/specs/README.md`). Ideas deferred from a spec are added under "Ideas not yet scheduled".

**Product scope** (from `CLAUDE.md`): staff circulation and catalogue workflows on top of Koha. Anything outside that scope needs the scope changed first, as a Level 2 change to `CLAUDE.md`.

The order below is a proposal. The maintainer decides it.

| # | Work | Status | Why / notes |
| --- | --- | --- | --- |
| — | Foundation | proposed | Not a spec. The solution, and the three projects with their test projects, as ADR 0003 describes. `.editorconfig`, the official Koha contract, and the NSwag tool and configuration are already in place. It adds dependencies, so it is a Level 2 change. It adds no behavior. |
| 001 | API error contract | proposed | Behavior every capability shares: the status mapping, the Problem Details shape, masked server failures, logging with the trace identifier, and the startup check (`ER-004` to `ER-007`, `ER-011`). |
| 002 | Koha connection | proposed | Behavior every Koha call shares: authentication, token handling, the single retry, the deadline, and cancellation (`EX-004` to `EX-013`). |
| 003 | The first staff capability | to be decided | The maintainer chooses it. |

## Ideas not yet scheduled

The maintainer orders these. None is approved.

- **Staff authentication.** Until it exists, the API is open to anyone who can reach it.
- **A health endpoint,** for container and infrastructure monitoring.
- **Circulation and catalogue workflows,** for example item lookup, check out, check in, renew, holds, item search, and record edits.
- **Mutation testing,** to measure how strong the tests are. It needs a new tool, so it is Level 2.
