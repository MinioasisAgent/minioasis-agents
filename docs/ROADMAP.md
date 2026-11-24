- **Access:** Level 1
- **Last Updated:** 2026-11-24

# Roadmap

Planned and completed work, in intended order. A row with a brief has the status of its brief: the `Status:` line at the top of the brief is the only place it is written (`docs/process.md`, section 1). A row without a brief is a proposal. Ideas deferred from a brief are added under "Ideas not yet scheduled".

**Product scope** (from `CLAUDE.md`): staff circulation and catalogue workflows on top of Koha. Anything outside that scope needs the scope changed first, as a Level 2 change to `CLAUDE.md`.

The order below is a proposal. The maintainer decides it.

| # | Work | Brief | Why / notes |
| --- | --- | --- | --- |
| — | Foundation (done) | none | Not a capability, so it has no brief. The solution, and the three projects with their test projects, as `AR-001` and `TS-004` describe, with xUnit v3 as the test framework. It added dependencies, so it was a Level 2 change. It added no behavior. |
| 001 | Get item | `docs/briefs/001-get-item.md` | Look up one Koha item by its item number. On 2026-11-23 the maintainer put the two shared parts into this brief: the API error contract (`ER-004` to `ER-007`, `ER-011`) and the Koha connection (`EX-004` to `EX-013`). |
| 002 | Staff access control | not written yet | The API validates the Cloudflare Access token that nginx passes on (`FE-004`), and listens on localhost only. The maintainer's design, 2026-11-23. Until it is built, the API is open to anyone who can reach it. |
| — | Move the backend into `backend/` | none | Not a capability. The solution, the projects, and their tests move, and CI, the scripts, `AR-001`, and `docs/running.md` follow. It is a Level 2 change. The maintainer's decision, 2026-11-24. |
| — | Frontend foundation | none | Not a capability. It creates `web/` with the tools of `FE-001`, the frontend's checks and CI job, an example nginx configuration, and `contracts/minioasis.openapi.json` with the existing route. It adds the frontend's checks to `docs/process.md`, section 13, and how to run both components to `docs/running.md`. It is a Level 2 change. The maintainer chooses when. |
| 003 | The first staff capability | not written yet | The maintainer chooses it. |

## Ideas not yet scheduled

The maintainer orders these. None is approved.

- **A health endpoint,** for container and infrastructure monitoring.
- **Item lookup by barcode,** and item status, availability, and location. Deferred from brief 001.
- **Circulation and catalogue workflows,** for example check out, check in, renew, holds, item search, and record edits.
- **Mutation testing,** to measure how strong the tests are. It needs a new tool, so it is Level 2.
