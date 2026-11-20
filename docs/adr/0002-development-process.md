- **Access:** Level 2
- **Last Updated:** 2026-11-20

# ADR 0002: Development process adapted from GSD Core and brave-core

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** Maintainer

## Context

The maintainer wants a development process for AI agents that is sustainable, safe, and reliable rather than fashionable, and that produces high-quality code without needing the maintainer's review of every change.

Two open-source projects were studied by reading their files, without installing or running anything:

- **GSD Core** (formerly "Get Shit Done"), reviewed at version 1.20.1 (MIT licence). It is a toolkit that drives AI coding agents through a loop of discuss, plan, execute, and verify.
- **brave-core** (`brave/brave-core`), reviewed at commit `9bf56d6` of 16 November 2026. It is a large codebase that uses AI agents with a library of written best practices and a review that checks them.

## Decision

Do not install either project's tooling. Adopt their strongest ideas as plain rules, templates, and two small skills owned by this repository. The ideas are restated in this project's own words; no text or code from either project is included.

### Ideas adopted from GSD Core

| Idea | Where it lives here |
| --- | --- |
| Discuss before planning; record decisions as locked, agent's discretion, or deferred | Spec rules in `docs/specs/README.md`; "Decisions" in `spec.md` |
| Probe edge cases and prohibitions before approval | "Edge cases" and "Prohibitions" in `spec.md` |
| Check the plan with a second agent before code exists, including a check for quietly reduced scope | Step 5 in `docs/specs/README.md` |
| Flag one-way decisions for a human; leave two-way ones to the agent | `CLAUDE.md`; "One-way decisions" in `plan.md` |
| Rules for unplanned work: fix bugs and blockers, stop for structural changes | `docs/workflow/implementation.md` |
| Never substitute a package that fails to install | `docs/workflow/implementation.md` |
| Map every requirement to an automated test before building | "Tests" in `plan.md`; `verification.md` |
| A test must be able to fail, and say how | "Fails when" in `plan.md`; "Seen failing" in `verification.md`; `TS-001` |
| Verify the goal backwards: exists, real, wired | `docs/workflow/verification.md`; "Wiring" in `verification.md` |
| A verifier abstains instead of guessing, and routes the item to a human | `docs/workflow/verification.md`; the "needs maintainer" result |
| Bounded review loops that escalate | The round limits in `docs/workflow/verification.md` |
| Fresh agents for checking, so the author does not grade their own work | `docs/workflow/verification.md` |
| Hand-off notes so that a new session can resume | `docs/workflow/implementation.md`; `docs/STATE.md` |
| Human acceptance testing recorded in the user's own words | "Manual acceptance" in `verification.md` |

### Ideas adopted from brave-core

| Idea | Where it lives here |
| --- | --- |
| A library of best practices, each with a stable ID, a reason, and good and bad examples | `docs/best-practices/` |
| Rules grow from real review findings, not from speculation | "Adding, changing, and retiring a rule" in `docs/best-practices/README.md` |
| A review that gives every applicable rule a verdict, so that none is skipped | `.claude/skills/review/SKILL.md`; `docs/workflow/verification.md` |
| A second, independent pass that assumes the first may be wrong | `.claude/skills/verify-spec/SKILL.md` |
| A short entry file that points to the documents a task needs | `CLAUDE.md`, section 6 |
| Scripts for mechanical steps | `scripts/preflight.sh`, `scripts/preflight.ps1` |

### Ideas not adopted

| Idea | From | Why not |
| --- | --- | --- |
| Installing the toolkit (Node.js, helper programs, hooks, about 60 agent definitions) | GSD Core | A fast-moving third-party dependency that steers every agent action, with frequent releases and no long-term-support version. |
| Running without asking ("yolo" mode) | GSD Core | Conflicts with Level 2 in `CLAUDE.md`. |
| Numeric ambiguity scores | GSD Core | False precision. A short readiness checklist does the same job. |
| Parallel execution "waves" | GSD Core | This project builds one spec at a time so that each transfer is complete. |
| Machine-readable plan formats | GSD Core | Only useful with GSD's tooling. Plans here are plain Markdown tables. |
| Milestone archives, statistics, and user profiling | GSD Core | Not needed at this size. |
| A review bot that runs on a schedule and posts comments | brave-core | It serves a stream of outside contributors, which this project does not have. |
| An evaluation harness that tests the skills themselves | brave-core | Costly to build and run for a project of this size. |
| Agents running unattended in CI | brave-core | No task here needs it, and it would act without the levels in `CLAUDE.md`. |
| A whole-codebase audit of test quality by many agents | brave-core | The codebase is small enough for review and spec verification to cover it. |
| Splitting rules into chunks for many sub-agents | brave-core | Needed for hundreds of rules. One reviewer can check this library. |

## Consequences

- The process has no dependency to install, upgrade, or lose.
- The entry file is `CLAUDE.md`, which Claude Code loads at the start of every session and into the sub-agents a session starts. The two skills are plain Markdown, so they can also be read and followed directly.
- Expectations about code are written once, with examples, and cited by ID in reviews, so the maintainer reviews a rule once and not the same mistake in every pull request.
- Only part of the process is enforced by tools: the preflight checks, which CI runs on every pull request, and code-owner review of the safeguard files. The rest is followed because the rules say so, and is checked at review and at transfer.
- Both source projects keep changing. Their ideas can be reviewed again later and adopted the same way, as Level 2 changes.
