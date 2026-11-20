---
name: review
description: Review a pull request or local changes against every applicable rule in docs/best-practices, and against the governing spec. Gives each rule a PASS, N/A or FAIL verdict. Read-only. Use when asked to review a pull request, a branch, or uncommitted changes.
---

- **Access:** Level 2
- **Last Updated:** 2026-11-20

# Review

The procedure for a review. The rules it serves are in `docs/workflow/verification.md`.

## Ground rules

- **Run as the `reviewer` role,** which sets the model and the default effort. If you are running as anything else, say so before you start.
- **If you wrote the change, say so and stop.**
- **Read-only:** do not edit, commit, push, merge, or post.
- **Work from the code and the spec,** not from the author's explanation.
- **Judge only what the change adds or modifies** ("Existing code" in `docs/best-practices/README.md`).

## Step 1: read the change

For a pull request, its diff against the base branch. For local work, the commits on the branch since it left `master`, plus uncommitted changes. List the changed files and read the whole diff. Do not shorten or sample a large one.

## Step 2: choose the rule files

Use "Which rules apply to a change" in `docs/best-practices/README.md`. When unsure whether a file applies, include it.

## Step 3: audit every rule

Go through each chosen file from its first rule to its last, and give every rule exactly one verdict:

- `PASS`: you checked the change against the rule and found no violation.
- `N/A`: the rule does not apply to this kind of change.
- `FAIL`: the change breaks the rule. Give the file, the line, and one sentence of reason.

List every rule, including those that pass: the complete list shows that none was skipped. Where a rule is checked by a tool, run it (for example `scripts/preflight.sh`) and use its result. If it cannot run, say exactly why, and do not record a `PASS`.

## Step 4: check the behavior

If a spec governs the change:

- Does it do what the requirements it claims to implement say, and nothing the spec does not describe?
- Does every new or changed behavior have a test that would fail if the behavior broke (`TS-001`, `TS-012`)?

If no spec governs it, check that it matches the level it claims in `CLAUDE.md`. Public behavior changed without a spec is a blocking finding.

## Step 5: report

```text
REVIEW of <pull request or branch> at <commit>

RULE AUDIT
architecture.md
  PASS  AR-001
  N/A   AR-002
  FAIL  AR-004
  …
code-design.md
  …

VIOLATIONS
- AR-004  Minioasis.Api/Loans/Endpoints/LoanEndpoints.cs:31
  The endpoint decides the status code itself and does not use the mapper.

BEHAVIOR
- <finding, with the requirement it concerns, and where it started if not in this change: the plan or the spec>, or "No findings"

CHECKS
- scripts/preflight.sh: passed | failed | not run, because …

NOT COVERED BY A RULE
- <a problem that no rule covers, with a proposed rule title>, or "Nothing"

VERDICT: No blocking issues | Blocking issues found
```

Any `FAIL`, and any behavior that departs from the spec, is blocking. Propose a missing rule under "NOT COVERED BY A RULE"; do not add it yourself.
