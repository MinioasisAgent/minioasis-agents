- **Access:** Level 2
- **Last Updated:** 2026-11-20

# <NNN> — <Capability name>: verification

- **Spec:** [spec.md](spec.md)
- **Verified at commit:** `<sha>`, or "not yet"
- **Transfer branch:** `ready/<NNN>-<name>`, or "not yet"

## Clause checklist

<Written by the author before verification, from the words of `spec.md` (`docs/workflow/verification.md`). One row for each clause: each part of each Given / When / Then, each named input class or value, each covered edge case and prohibition. For an open class, list the members tested.>

| Spec clause | Cases tested | Proving test (`Project::Class::Method`) |
| --- | --- | --- |
| R1: <clause> | | |

## Requirement coverage

- **Proves:** what the test asserts, in plain English, so the maintainer can review behavior without reading C#.
- **Seen failing:** written before the code, or the behavior broken on purpose (say how) and restored.
- **Result:** ✅ pass, ❌ fail, ⚠️ gap (no test), or 🧑 needs maintainer (the tests cannot show it). The spec cannot be `verified` while any row is a fail or a gap. Every "needs maintainer" row appears in manual acceptance.

| Req | Proves (Given / When / Then) | Test (`Project::Class::Method`) | Seen failing | Result |
| --- | --- | --- | --- | --- |
| R1 | | | | |

## Edge cases and prohibitions

| Item from the spec | State | Proven by |
| --- | --- | --- |
| | covered / dismissed / manual | <test, reason, or manual scenario number> |

## Wiring

<Where each new piece is registered for dependency injection, which endpoint reaches it, and the tests that prove both (`TS-005`).>

| Piece | Registered in | Reached from | Proven by |
| --- | --- | --- | --- |
| | | | |

## Checks

| Commit | Date | `scripts/preflight.sh` | Tests passed / total |
| --- | --- | --- | --- |
| | | passed / failed | |

## Independent verification

<The current result only, not a history of rounds.>

- **Verdict:** verified | not verified
- **Blocking findings:** <each, and how it was resolved>
- **Non-blocking findings:** <each, and where it was recorded for follow-up>

## Manual acceptance (maintainer)

<Agents write the scenarios and the expected results. The rest comes from the maintainer's report, word for word (`docs/specs/README.md`, rule 8).>

- **Koha instance and version:**
- **Date:**

| # | Scenario | Expected | Observed (maintainer's words) | Result |
| --- | --- | --- | --- | --- |
| 1 | <happy path> | | | pass / issue / skipped |
| 2 | <one failure case> | | | |

An "issue" becomes a fix in this repository, followed by a new `ready/` branch.
