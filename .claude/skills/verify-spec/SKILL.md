---
name: verify-spec
description: Independently verify a fully built spec against the fixed bar, by breaking each stated behavior once in a scratch copy and confirming that a test fails. Never modifies the repository. Use when asked to verify a spec or to decide whether it can be marked verified.
---

- **Access:** Level 2
- **Last Updated:** 2026-11-20

# Verify a spec

The procedure for verification. The rules are in `docs/workflow/verification.md`.

## Ground rules

- **Run as the `verifier` role,** which sets the model and the default effort. If you are running as anything else, say so before you start.
- **If you wrote the code or its tests, say so and stop.**
- **Work from `spec.md` and the code.** Treat `verification.md` as the author's claim to check, not as evidence.
- **Never modify the repository.** Experiment only in a scratch copy outside it, and delete the copy when you finish. Do not commit, push, or post.
- **Never edit generated code or `contracts/official_koha_openapi.json`,** not even in the copy.
- **Report a blocking finding only against the bar.** Everything else is non-blocking.

  > Every explicit Given / When / Then, every covered edge case, and every prohibition in the spec has at least one passing test that fails when that stated behavior is directly broken.

  For a clause that covers an open class, such as "any" or "every", tests of several members including the edges are enough.
- **Never record a pass for what tests cannot show.** What only a real Koha can show is "needs maintainer".

## Step 1: list the clauses

Read `spec.md` in full and list its clauses yourself: every part of each Given / When / Then, every covered edge case, every prohibition. A clause that the checklist in `verification.md` misses, or lists without a test, is a blocking finding.

## Step 2: break each clause once

In a scratch copy of the repository, for each clause:

1. Make the simplest change to the production code that makes the stated behavior false. Use your own change, not one the author recorded.
2. Rebuild and run the tests with a hang timeout (`dotnet test … --blame-hang-timeout 60s`).
3. Confirm that at least one test fails, and for that reason.
4. Restore the code.

In a second round (`docs/workflow/verification.md`), break only the clauses that the fix could affect, judged from its diff; the full test suite still runs in step 4.

If no test fails, the clause is a blocking finding. If a test only hangs, count it as failing and add a non-blocking finding that it should fail cleanly (`TS-007`).

## Step 3: confirm the wiring

The capability is registered for dependency injection and reachable from its endpoint, and tests prove both (`TS-005`).

## Step 4: run the checks

On unmodified source, run `scripts/preflight.sh`. Run any timing-based tests at least five times.

## Step 5: check the record

In `verification.md`: the tests it names exist, its counts match, nothing that only a real Koha can show is recorded as a pass, and every "needs maintainer" item has a manual acceptance scenario.

## Step 6: report

```text
VERIFICATION of spec <NNN> at <commit>

CLAUSES
| Clause | Proving test | My change | Test failed? |
| R1: … | … | … | yes / no |

BLOCKING FINDINGS
- <clause with no failing test, and where the problem started: the spec, the plan, or the code and tests>, or "None"

NON-BLOCKING FINDINGS
- <anything else>, or "None"

CHECKS
- scripts/preflight.sh: passed | failed
- Repeated timing-based tests: <n> of <n> passed

NEEDS MAINTAINER
- <what only a real Koha can show>, or "Nothing"

VERDICT: VERIFIED
```

or, as the last line, `VERDICT: NOT VERIFIED — <reason>`.
