- **Access:** Level 1 <!-- For the copy: Level 2 once approved (`docs/process.md`, section 3). This template itself is Level 2. -->
- **Last Updated:** <date> <!-- Template last updated: 2026-11-23 -->

# <NNN> — <Capability name>

- **Status:** draft <!-- draft, approved, built, transferred, or rejected -->
- **Changes data in Koha:** yes | no <!-- yes: part 8 is required -->
- **Related:** <other briefs, issues>

## 1. Goal

<One or two sentences, specific enough to check: "Staff can X, so that Y." Not "improve X".>

## 2. What people do and see

1. …

## 3. The API

| Method | Route | Success | Request | Response fields and types |
| --- | --- | --- | --- | --- |
| | | | | |

| Situation | HTTP status | Public code | Title / detail shown to staff |
| --- | --- | --- | --- |
| | | | |

<Internal codes that are only logged. Configuration names. The Koha operations used, each marked read or write, with the focused contract. Write "None" where there are none.>

<Who may call it. What the Koha service account must be allowed to do (`EX-005`). Which patron data is returned. That no patron data is logged, or what is logged and why.>

## 4. Decisions

**Made by the maintainer**

| Decision | Reason | Date |
| --- | --- | --- |
| | | |

**Made by the agent**

<Every choice about Koha, security, a standard, or anything one-way. Evidence is a result from the test Koha with its Koha version, Koha's source at the version the maintainer runs, or the standard.>

| Decision | Alternative rejected | Reason and evidence |
| --- | --- | --- |
| | | |

**Needs your decision**

<Each entry with the agent's recommendation and reason. Must be empty before the brief check (`docs/process.md`, section 3, rule 1).>

**Least sure of**

<What the author would check first if this capability misbehaved.>

## 5. Not included

- <What this capability deliberately does not do, and why.>

## 6. Behaviors that must have a test

<One line each, numbered and never renumbered. Include what the system must never do. Mark the checks that run against the test Koha (`TS-013`).>

1. …

## 7. How it will be built

<The approach in a few sentences: how the capability fits `docs/best-practices/architecture.md`.>

| Project | Package | Version | Why |
| --- | --- | --- | --- |
| | | | |

<The files by project, in a line or two each. The focused contract and the generation command (`EX-003`), if any.>

## 8. Failure and retry

<Only when the capability changes data in Koha. Otherwise delete this part. What happens if Koha fails, times out, or partly succeeds. Whether the action is safe to repeat. What staff see, and what they should do.>

## Acceptance

<Added at transfer: the maintainer's report, word for word, with the Koha version and the date, or the recorded skip (`docs/process.md`, section 9).>
