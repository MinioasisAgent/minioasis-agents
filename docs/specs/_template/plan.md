- **Access:** Level 2
- **Last Updated:** 2026-11-20

# <NNN> — <Capability name>: implementation plan

- **Spec:** [spec.md](spec.md)
- **Plan status:** draft | checked | approved | in progress | complete

## Approach

<How the capability fits `docs/best-practices/architecture.md`, and the key design choices, including those made under "Agent's discretion".>

## One-way decisions

<Every one-way decision (`CLAUDE.md`, section 5), each settled by a locked decision or a requirement of the spec. "None" if there are none.>

| Decision | What undoing it would break | Settled by |
| --- | --- | --- |
| | | D<n> / R<n> |

## Files

| Project | File | New or changed | Purpose |
| --- | --- | --- | --- |
| `Minioasis.Application` | | | |
| `Minioasis.Koha` | | | |
| `Minioasis.Api` | | | |
| Tests | | | |

## Contracts and generation

<The focused contract created or changed, the operations it copies from `contracts/official_koha_openapi.json`, and the generation command (`EX-003`). "None" if not applicable.>

## Error definitions

| Code | Category | Client-visible resource entry |
| --- | --- | --- |
| | | |

## Tests

<At least one planned test for every requirement and every covered edge case and prohibition, on the component that owns the behavior (`TS-002`). "Fails when" says what would make it fail.>

| Requirement | Layer | Planned test | Fails when |
| --- | --- | --- | --- |
| R1 | Application / Api / Koha | `<Class>::<Method>` | |

## Pull request breakdown

<Small steps, each finishable in one session and mergeable on its own, tests first.>

1. …

## Risks

- <What could go wrong, and how the plan reduces it.>

## Plan check

<The reviewer's findings against the plan-check questions in `docs/specs/README.md`, and how each was resolved.>

## Deviations

<Unplanned fixes and additions made during implementation, and revisions of this plan sent back by a later check, each with its reason (`docs/workflow/implementation.md`).>
