- **Access:** Level 1
- **Last Updated:** 2026-11-22

# Roadmap

Planned and completed work, in intended order. Agents may add `proposed` rows and update the status column. Only the maintainer's acceptance of a brief moves it to `approved` (`docs/process.md`, section 5). Ideas deferred from a brief are added under "Ideas not yet scheduled".

**Product scope** (from `CLAUDE.md`): staff circulation and catalogue workflows on top of Koha. Anything outside that scope needs the scope changed first, as a Level 2 change to `CLAUDE.md`.

The order below is a proposal. The maintainer decides it.

| # | Work | Status | Why / notes |
| --- | --- | --- | --- |
| — | Foundation | done | Not a capability, so it has no brief. The solution, and the three projects with their test projects, as `AR-001` and `TS-004` describe, with xUnit v3 as the test framework. It added dependencies, so it was a Level 2 change. It added no behavior. |
| 001 | API error contract | proposed | Behavior every capability shares: the status mapping, the Problem Details shape, masked server failures, logging with the trace identifier, and the startup check (`ER-004` to `ER-007`, `ER-011`). |
| 002 | Koha connection | proposed | Behavior every Koha call shares: authentication, token handling, the single retry, the deadline, and cancellation (`EX-004` to `EX-013`). |
| 003 | The first staff capability | to be decided | The maintainer chooses it. |

## Ideas not yet scheduled

The maintainer orders these. None is approved.

- **Staff authentication.** Until it exists, the API is open to anyone who can reach it.
- **A health endpoint,** for container and infrastructure monitoring.
- **Circulation and catalogue workflows,** for example item lookup, check out, check in, renew, holds, item search, and record edits.
- **Mutation testing,** to measure how strong the tests are. It needs a new tool, so it is Level 2.
