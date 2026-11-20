- **Access:** Level 2
- **Last Updated:** 2026-11-20

# Implementation

How an approved spec is built (step 6 of `docs/specs/README.md`). Changing this file is a Level 2 change.

## Before code

1. The spec and the plan are both `approved`.
2. Every one-way decision in `plan.md` is settled, as `CLAUDE.md` section 5 requires.
3. Each task goes to `coder-heavy` or `coder-light` (`CLAUDE.md`, section 10).

## While building

- Write each test first and watch it fail (`TS-001`), and keep the spec's `verification.md` current as tests are added.
- Deliver every requirement in full (`docs/specs/README.md`, rule 7).

**Fix without asking,** and list each fix under "Deviations" in the pull request and in `plan.md`:

- a bug in the code you are working on;
- a correctness or safety piece that a requirement implies but does not spell out, such as validation, error handling, or cancellation;
- a blocker, such as a build error or a broken reference.

When a later check sends work back to the plan (`docs/workflow/verification.md`), the revised plan lists the change under "Deviations", with the finding that caused it.

**Stop and escalate** with a Level 2 pull request when the work needs a new project or layer, a changed public contract, a new dependency, or a departure from the approved spec or plan (`CLAUDE.md`, section 5). Never implement the departure first.

**Missing packages and tools:** if one cannot be found or installed, stop and report it. Never substitute one with a similar name.

## One spec at a time

Do not merge behavior from a later spec into `master` until the current spec is `verified` and its `ready/` branch exists, so that every transfer contains only complete specs. Spec documents, independent Level 1 fixes, and accepted Level 2 changes that add no unfinished behavior may merge at any time.

## Pull request size and hand-off

Size each pull request so that one session can finish it, and limit it to one spec, one fix, or one task.

If a session ends with work unfinished, push the branch and end the draft pull request description with a "Hand-off" section: the current state, what remains, the decisions made and why, and the next action. A new session must be able to continue from that section and `docs/STATE.md` alone.
