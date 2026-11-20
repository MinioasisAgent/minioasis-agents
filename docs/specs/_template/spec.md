- **Access:** Level 2
- **Last Updated:** 2026-11-20

# <NNN> — <Capability name>

- **Status:** draft <!-- draft, reviewed, approved, verified, transferred, or rejected -->
- **Kind:** read | write (state-changing in Koha) | internal
- **Related:** <ADRs, other specs, issues>

## Goal

<One or two sentences, specific enough to check: "Staff can X, so that Y." Not "improve X".>

## Background

<What exists today in the code and in Koha, and what is missing, from the repository, not from assumptions.>

## Staff and roles

<Who uses this, for example circulation desk, cataloguer, or administrator, and in which situation.>

## Staff workflow

<Numbered steps from the staff member's point of view, without implementation detail.>

1. …

## Decisions

<Numbered and never renumbered. The kinds are defined in `docs/specs/README.md`.>

**Locked**

- **D1.** <decision> — <date>

**Agent's discretion** (the choice is recorded in `plan.md`)

- <area>

**Deferred** (added to `docs/ROADMAP.md`)

- <idea>

## Requirements

<As rule 4 of `docs/specs/README.md` requires. Shared behavior is referred to, not restated.>

- **R1.** Given <situation>, when <action>, then <observable result>.

## Edge cases

| Edge case | State | Requirement, reason, or manual scenario |
| --- | --- | --- |
| | covered / dismissed / manual | |

## Prohibitions

| The system must not… | State | Requirement, reason, or manual scenario |
| --- | --- | --- |
| | covered / dismissed / manual | |

## API contract

| Method | Route | Success | Request | Response |
| --- | --- | --- | --- | --- |
| | | | | |

<Response field names and types. Note anything nullable.>

## Errors

| Situation | HTTP status | Public code | Title / detail shown to staff |
| --- | --- | --- | --- |
| | | | |

<Each situation that causes a server failure, with its internal error code. Its response is always the masked one (`ER-006`).>

## Koha operations

| Koha operation | Read or write | Required Koha permission | Focused contract |
| --- | --- | --- | --- |
| | | | `contracts/<name>.openapi.json` |

## Koha configuration

<System preferences, circulation rules, or authorized values that change this behavior, or "None known".>

## Failure and retry

<Required for a write spec (rule 6 of `docs/specs/README.md`). What happens if Koha fails, times out, or partly succeeds; whether the operation is safe to retry; what staff see and should do.>

## Security and privacy

<Who may perform this operation; which patron personal data is read, returned, or logged; what must never be logged.>

| Threat | Mitigation | Requirement |
| --- | --- | --- |
| | | |

## Out of scope

- <What this spec deliberately does not do, and why.>

## Open questions

<Must be empty before the spec check.>

- …

## Ready for approval

- [ ] The goal is specific enough to check.
- [ ] Every requirement is testable, and the cases it covers can be listed.
- [ ] Every edge case and prohibition has a state.
- [ ] "Out of scope" names the nearest things this spec does not do.
- [ ] For a write spec, "Failure and retry" is complete.
- [ ] There are no open questions.
