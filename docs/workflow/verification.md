- **Access:** Level 2
- **Last Updated:** 2026-11-21

# Review and verification

How work is checked before it merges, and how a spec becomes `verified`. Changing this file is a Level 2 change.

| Check | When | Question | Role and procedure |
| --- | --- | --- | --- |
| **Review** | Every pull request | Does the change follow the rules, and do what its spec says? | `reviewer`, `.claude/skills/review/SKILL.md` |
| **Spec and plan checks** | Steps 2 and 5 of `docs/specs/README.md` | The questions listed there | `reviewer` |
| **Verification** | Once per spec, when fully built | Is every clause of the spec proven by a test? | `verifier`, `.claude/skills/verify-spec/SKILL.md` |

## Independence

The coordinator starts each check as a fresh session that did not write the work (`CLAUDE.md`, section 10), and gives it the spec and the code, not the author's explanation. The checker runs on the coders' model (ADR 0004), so it can share their blind spots: its check is a second look, not proof. The maintainer's spot-checks at transfer and the manual acceptance against a real Koha are the independent evidence.

## Review

The `reviewer` follows the review skill, which defines what blocks a merge. A change that works but breaks a rule does not pass. The outcome is recorded in the pull request.

## Verification

### The bar for `verified`

> Every explicit Given / When / Then, every covered edge case, and every prohibition in the spec has at least one passing test that fails when that stated behavior is directly broken.

"Directly broken" means the simplest change that makes the stated behavior false: a different status, an input class no longer rejected, a side effect no longer happening.

- A clause without such a test is a **blocking** finding.
- Anything else is **non-blocking** and recorded for follow-up: a contrived change that no test catches but that leaves every stated behavior intact, a concern about test quality, unreachable code.

The bar is fixed because a deeper search always finds one more change that no test catches.

The verifier also confirms that each requirement **exists** (it is implemented), is **real** (not a placeholder, a hard-coded answer, or an empty method), and is **wired** (registered for dependency injection and reachable from its endpoint, `TS-005`).

### Before asking for verification

The author writes the clause checklist in `verification.md` from the words of the spec, not from the tests that exist: that is what finds the missing tests. For an open class such as "any" or "every", it lists the members tested (`TS-006`). The author closes every gap, and records how each test was seen to fail: written before the code, or the behavior broken on purpose and restored (`TS-001`).

### What tests cannot show

How a real Koha behaves, or a case the spec does not decide, is never reported as a pass. Mark it "needs maintainer" and add it to the manual acceptance scenarios. A confident guess recorded as a pass breaks these rules.

### After verification

When the verdict is "verified" and the checks pass, the coordinator records the result in `verification.md` and sets the spec to `verified`, in one pull request. After it merges, the coordinator creates `ready/<NNN>-<name>` at the resulting `master` commit (`docs/workflow/git.md`).

## Stages and round limits

Work on a spec moves through three stages. A **round** is one pass of a stage's work followed by its check.

| Stage | One round | Limit before the maintainer decides |
| --- | --- | --- |
| **A. Spec** | The coordinator writes or changes `spec.md`, and the `reviewer` runs the spec check. | Two rounds per spec, the first included |
| **B. Plan** | The coordinator writes or changes `plan.md`, and the `reviewer` runs the plan check. The maintainer then accepts the plan before anything is built from it. | Two rounds per spec, the first included |
| **C. Build: review** | A coder writes or fixes one pull request, and the `reviewer` reviews it. | Three rounds per pull request, or fewer if a round does not reduce the blocking findings |
| **C. Build: verification** | A fresh `verifier` checks the spec. Before a second round, a coder fixes the findings and the `reviewer` reviews the fix; the second round re-checks only what the fix could affect. | Two rounds each time stage C starts from a new or revised plan |

The limits count the agents' own rounds. A revision that the maintainer asks for, when accepting a spec or a plan, does not count.

### Sending a problem back

For each blocking finding, the checker names the stage where it started: the spec (A), the plan (B), or the code and tests (C). The coordinator goes back to that stage, or to the earliest one if the findings name several, and the work moves forward again from there through every later check.

For example, the verifier finds a requirement whose planned test cannot fail where the plan puts it. The coordinator revises `plan.md`, the `reviewer` runs the plan check on the revised plan, the maintainer accepts it, and the coders build from it before verification starts again.

A change to the spec always needs a new round of B, because the plan must follow the spec. A change to an approved spec's requirements also needs the maintainer's acceptance (Level 2).

### Before the next spec or plan round

A fix can break what it does not touch. Before asking for the next round of stage A or B, the coordinator re-reads each changed requirement, decision, or plan entry in full, together with everything that refers to it. For a spec, it also repeats the re-read in "Spec check" (`docs/specs/README.md`). The next round runs the full check. The coordinator lists the changed sections by name only, without its reasoning.

### When a limit is reached

When the next round would pass a limit, the coordinator stops. It records the open findings, in `verification.md` for verification or in the pull request otherwise, labels the pull request `needs-human`, and the maintainer decides what comes next. The limits are starting values: revisit them once a few specs have been verified.
