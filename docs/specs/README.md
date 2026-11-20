- **Access:** Level 2
- **Last Updated:** 2026-11-20

# Specs

A spec is the agreed description of one capability: what staff need, how Minioasis behaves, and how that is proven. Agents build only what approved specs describe; the maintainer steers the product mainly by approving specs. Changing this file or `_template/` is a Level 2 change.

## The steps for one capability

| Step | What happens | Who | Output |
| --- | --- | --- | --- |
| 1. Discuss | Settle how the capability behaves ("Discussing a capability"). | Coordinator and maintainer | Decisions in `spec.md` |
| 2. Specify | Write requirements, edge cases, and prohibitions; then the spec check. | Coordinator, then `reviewer` | `spec.md`: `draft`, then `reviewed` |
| 3. Approve | The maintainer accepts the spec's pull request. | Maintainer | `approved` |
| 4. Plan | Map requirements to files and tests, and mark one-way decisions. | Coordinator | `plan.md` |
| 5. Check and approve the plan | Look for holes before any code exists ("Plan check"); then the maintainer accepts the plan's pull request. | `reviewer`, then maintainer | "Plan check" in `plan.md`; plan status `approved` |
| 6. Build | Tests first, then code, in small pull requests (`docs/workflow/implementation.md`). | Coders | Merged pull requests |
| 7. Verify | Check every clause against the code and tests (`docs/workflow/verification.md`). | `verifier` | `verification.md`, `verified`, `ready/<NNN>-<name>` |
| 8. Transfer | Review, test against a real Koha, merge into production. | Maintainer | `transferred` |

The maintainer acts at steps 1, 3, 5, and 8. Steps 4, 6, and 7 need no human unless a Level 2 change comes up. A fix that restores behavior a spec already describes skips these steps (Level 1 in `CLAUDE.md`).

## Rules

These are binding.

1. **Spec and plan before code.** New behavior is built only from an `approved` spec and its `approved` plan.
2. **No open questions.** A spec with an open question, or with an edge case or prohibition that has no state ("Edge cases and prohibitions"), cannot be reviewed or approved. Ask; do not guess.
3. **Every decision is recorded,** as locked, agent's discretion, or deferred ("Discussing a capability"). Reopening or narrowing a locked decision is a Level 2 change.
4. **Numbered, testable requirements.** Each is written as Given / When / Then and names the input classes or values it covers. Where an open class such as "any" cannot be avoided, state its boundaries (`TS-006`). Never renumber; mark a retired requirement `(removed)`.
5. **One capability per spec.** Behavior that several capabilities share is specified once, in its own spec, and referred to. Rules about how code is written belong in `docs/best-practices/`.
6. **Writes define failure and retry.** A spec that writes to Koha says what happens on partial failure and whether the operation may be retried, before it can be approved.
7. **Deliver in full.** No plan or pull request narrows an approved requirement with a "simplified", "for now", "placeholder", or "basic" version. Deliver it, or propose splitting the spec (Level 2).
8. **Manual acceptance comes from the maintainer.** It is required for a spec that writes to Koha; for any other spec the maintainer may skip it, and the skip is recorded. Agents record the maintainer's report word for word and never fill in results themselves.

## Status

```text
draft ──► reviewed ──► approved ──► verified ──► transferred
  │
  └──► rejected
```

| Status | Set when | Notes |
| --- | --- | --- |
| `draft` | The spec is being discussed and written. | |
| `reviewed` | The `reviewer`'s spec check found no blocking issue. | Only now is the maintainer asked to approve. |
| `approved` | The maintainer accepted the spec's pull request (`CLAUDE.md`, section 5). | Set in that pull request before merging it or, if the maintainer merged it, in the next pull request for the spec. Writing it without acceptance breaks the rules. |
| `verified` | The independent verification met the bar in `docs/workflow/verification.md`, and the checks pass. | |
| `transferred` | The maintainer reported the merge into production and the manual acceptance result, or its skip (rule 8). | |
| `rejected` | The maintainer decided not to pursue it. | The reason is recorded in `spec.md`; the folder and number are kept. |

The coordinator sets every status. The `Status:` line at the top of `spec.md` is the only source; `docs/STATE.md` and `docs/ROADMAP.md` repeat it and are updated in the same pull request.

## Folder and numbering

```text
docs/specs/<NNN>-<short-name>/
├── spec.md           what and why: reviewed, then approved by the maintainer
├── plan.md           how: written by the coordinator, checked by the reviewer, approved by the maintainer
└── verification.md   proof: clause → test → result, plus manual acceptance
```

Start from copies of the files in `_template/`, and set each copy's header (`docs/workflow/git.md`): `spec.md` is Level 1 until the spec is `approved` and Level 2 from then on, except that its status line and wording fixes stay Level 1; `plan.md` is Level 1 until it is `approved` and Level 2 from then on, except that its status line and its "Deviations" record stay Level 1; `verification.md` is Level 1. Numbers have three digits, are assigned in order, and are never reused. The short name is lowercase kebab-case and names the capability, for example `<NNN>-renew-loan`.

## Discussing a capability (step 1)

- Let the maintainer describe the capability in their own words, then ask for a walk-through: "A patron is at the desk with a book. What does the staff member do, and what do they see?"
- Challenge vague words: "quickly", "simple", and "the usual way" each need a concrete meaning.
- Offer choices where the maintainer may not know the options: "When Koha refuses, should staff see Koha's reason or a general message?"
- Stop when you know what staff do, what they see, what happens when things go wrong, and what is out of scope.

Record each outcome under "Decisions" in `spec.md` as one of:

- **Locked:** the maintainer decided.
- **Agent's discretion:** the maintainer said "you decide". The choice is recorded in `plan.md`.
- **Deferred:** a good idea that is not part of this spec. It goes to `docs/ROADMAP.md`.

## Edge cases and prohibitions (step 2)

For each requirement, ask what could go wrong at its edges (empty, zero, very large, repeated, simultaneous, half-finished) and what the system must never do (log a secret, repeat a write, reveal internal details). Each item ends in exactly one state:

| State | Meaning |
| --- | --- |
| Covered | A numbered requirement decides it, so a test will prove it. |
| Dismissed | It cannot happen or does not matter here; the reason is written down. |
| Manual | Only a real Koha can show it; it becomes a manual acceptance scenario. |

## Spec check (step 2)

The `reviewer` reads `spec.md` alone and answers:

1. Does the spec meet every rule above?
2. Is every box under "Ready for approval" true?
3. Could two readers build different behavior from any requirement?

## Plan check (step 5)

The `reviewer` reads `spec.md` and `plan.md`, not the author's reasoning, and answers:

1. Does every requirement, covered edge case, and covered prohibition have planned work and a planned test?
2. Does the plan deliver every locked decision and requirement in full (rule 7)?
3. Does anything in the plan go beyond the spec?
4. Does the plan follow `docs/best-practices/`?
5. Is every one-way decision marked and settled by the spec (`CLAUDE.md`, section 5)?
6. Can every planned test actually fail?

Findings go back to the coordinator, within the round limits in `docs/workflow/verification.md`. When the check finds no blocking issue, the coordinator sets the plan status to `checked` and asks the maintainer to accept the plan's pull request (Level 2). After acceptance it sets the status to `approved`.
