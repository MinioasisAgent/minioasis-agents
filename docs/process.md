- **Access:** Level 2
- **Last Updated:** 2026-11-23

# Process

How a capability is built, from the first discussion to the branch that the maintainer transfers. Also how every change is branched, checked, reviewed, and merged. The transfer to production is in `docs/transfer.md`. Changing this file is a Level 2 change.

A **brief** is one document that describes one **capability**: what staff need, how Minioasis behaves, and how it will be built. Agents build only what approved briefs describe. The maintainer steers the product mainly by approving briefs.

## 1. The eight steps

| Step | What happens | Who | Result |
| --- | --- | --- | --- |
| 1. Discuss | Settle what the capability does (section 2). | Coordinator and maintainer | Decisions |
| 2. Write the brief | Fill in every part of the brief (section 3). | Coordinator | Status `draft` |
| 3. Check the brief | A new `reviewer` looks for the decision that is most likely wrong, and for holes (section 4). | `reviewer` | Findings, or none |
| 4. Approve | The maintainer accepts the brief's pull request (section 5). | Maintainer | Status `approved` |
| 5. Build | One coder builds the whole capability, tests first (section 6). A new `reviewer` reviews it (section 7). | `coder`, then `reviewer` | A pull request |
| 6. Verify | Every capability, after its review (section 8). | `verifier` | The result, in the pull request |
| 7. Finish | Set the status, merge, and mark the commit that the maintainer transfers (section 9). | Coordinator | Status `built`, branch `ready/<NNN>-<short-name>` |
| 8. Transfer | The maintainer tests the running capability and merges it into production (`docs/transfer.md`). | Maintainer | Status `transferred` |

The maintainer acts at steps 1, 4, and 8. The other steps need no human unless a Level 2 change comes up.

**Brief before code.** New behavior is built only from an `approved` brief. Two kinds of work need no brief:

- A fix that restores behavior which a brief or existing tests already describe. It is Level 1 (`CLAUDE.md`, section 3).
- A spike: throwaway code or commands that the coordinator runs against the test Koha (`TS-013`) to learn how Koha behaves before the brief is written. A spike lives in a scratch directory outside the repository. It is never pushed or merged. The brief records what it showed.

Why: a decision about Koha must rest on what Koha does. The coordinator can learn that only by asking Koha.

### Status

```text
draft ──► approved ──► built ──► transferred
  │
  └──► rejected
```

| Status | Set when | Notes |
| --- | --- | --- |
| `draft` | The brief is being discussed, written, or checked. | |
| `approved` | The maintainer accepted the brief's pull request (`CLAUDE.md`, section 3). | Set in that pull request before it merges. If the maintainer merged it, set in the next pull request for the capability. Writing `approved` without acceptance breaks the rules. |
| `built` | Every behavior in part 6 is built. The review found no blocking issue. The verification passed. The checks pass, the test Koha checks included. | Set as section 9 describes. |
| `transferred` | The maintainer reported the merge into production, and the result of the manual acceptance or that it was skipped (section 9). | |
| `rejected` | The maintainer decided not to pursue the capability. | The brief records the reason. The file and the number are kept. |

The coordinator sets every status. The `Status:` line at the top of the brief is the only source. `docs/ROADMAP.md` repeats it and is updated in the same pull request.

## 2. Discuss

1. Let the maintainer describe the capability in their own words. Then ask for a walk-through: "A patron is at the desk with a book. What does the staff member do, and what do they see?"
2. Challenge vague words. "Quickly", "simple", and "the usual way" each need a concrete meaning.
3. Find out from the test Koha (`TS-013`) how Koha's API behaves: authentication, request shapes, status codes. Do not guess. Where the test Koha cannot answer, or is not set up, use Koha's source at the version the maintainer runs. If neither answers, ask the maintainer.
4. Ask the maintainer about anything that depends on the library's own policy or Koha configuration, such as circulation rules.

   Why: the test Koha shows its own settings, not the library's.
5. **Recommend before you ask.** When the maintainer must choose, state the options, the one you recommend, and why. Then ask. When you think a decision that the maintainer already made is wrong, say so, and give the reason and the evidence. The maintainer decides.
6. Stop when you know four things: what staff do, what they see, what happens when things go wrong, and what is not included.

A good idea that is not part of this capability goes to `docs/ROADMAP.md`.

## 3. Write the brief

Copy `docs/briefs/TEMPLATE.md` to `docs/briefs/<NNN>-<short-name>.md`.

- **The number** has three digits. Numbers are assigned in order and never reused.
- **The short name** is lowercase kebab-case and names the capability, for example `<NNN>-renew-loan`.
- **The header** (section 14): the brief is Level 1 until it is `approved`, and Level 2 from then on. Its status line, wording corrections, and "Acceptance" stay Level 1.

Keep the brief short enough to read in one sitting, about three pages. If it grows well past that, the capability is probably two capabilities.

| Part | Holds |
| --- | --- |
| 1. Goal | What staff can do afterwards, in one or two sentences. |
| 2. What people do and see | The walk-through, step by step. The maintainer tests the finished capability by following it. |
| 3. The API | Routes, requests, response fields, error codes, and the text staff see. Who may call it. What the Koha service account must be allowed to do (`EX-005`). Which patron data is returned. |
| 4. Decisions | Each decision with who made it and why. What still needs the maintainer's decision. What the author is least sure of. |
| 5. Not included | What the capability deliberately leaves out. |
| 6. Behaviors that must have a test | One numbered line each, including what the system must never do. |
| 7. How it will be built | The approach, every new package with its version and reason, and the files by project. |
| 8. Failure and retry | Only for a capability that changes data in Koha. |

Five rules for the brief:

1. **Leave no open question.** A brief with an entry under "Needs your decision" cannot go to the brief check. Ask. Do not guess.
2. **Record every decision, with who made it and why.** For a decision that the agent made about Koha, security, a standard, or anything one-way (`CLAUDE.md`, section 3), the brief also names the alternative that was rejected, and the evidence. Evidence is one of three things: a result from the test Koha with its Koha version, Koha's source at the version the maintainer runs, or the standard. "The contract says so" is not evidence that no better way exists (`EX-001`). Changing a recorded decision after approval is a Level 2 change.

   Why: a check can ask whether a decision is right only when the decision and its evidence are written down.
3. **Number the behaviors, and make each one testable.** Part 6 lists each behavior that must have a test, one line each. Where the words are open, such as "any", the line names the inputs it covers (`TS-006`). Never renumber. Mark a retired line `(removed)`.
4. **Write one capability in one brief.** Behavior that several capabilities share is described once, in its own brief, and the other briefs refer to it. Rules about how code is written belong in `docs/best-practices/`.
5. **Define failure and retry for a capability that changes data in Koha.** Its brief fills in part 8 before it can be approved.

   Why: a request that changes data may already have changed a record in Koha when its response is lost (`EX-010`).

## 4. Check the brief

The coordinator starts a new `reviewer` and gives it the brief, not the coordinator's own explanation. The reviewer answers the two questions of the brief check. The questions, and what counts as blocking, are in `.claude/agents/reviewer.md`.

Why: the checks of the first capability asked only whether the documents were complete and consistent. A wrong decision, recorded consistently, passed every check. This check asks whether the decisions are right.

After the check:

1. Findings go back to the coordinator, within the round limits of section 10.
2. A question that the check raises for the maintainer is put to the maintainer with the pull request. The answer is written into the brief before its status becomes `approved`.
3. When the check finds no blocking issue, the coordinator asks the maintainer to accept the brief's pull request. The pull request is Level 2. Its description leads with any question that the check raised for the maintainer, then what the author is least sure of. What the reviewer challenged, and how it was settled, goes under "Review".

## 5. Approve

The maintainer accepts the brief's pull request in one of the three ways that `CLAUDE.md`, section 3, lists. One acceptance covers the decisions, the API, the packages, and how the capability will be built. The coordinator then sets the status to `approved`, as the status table in section 1 says.

## 6. Build

### Before code

1. The brief is `approved`.
2. The coordinator gives the whole capability to one `coder`. `CLAUDE.md`, section 4, rule 5, says when a second coder may start.

### While building

1. **Write each test first and watch it fail** (`TS-001`).
2. **Deliver every behavior in part 6 in full.** No pull request narrows an approved behavior with a "simplified", "for now", "placeholder", or "basic" version. Deliver the behavior, or propose splitting the brief, which is Level 2.
3. **Check against the test Koha** (`TS-013`) wherever the work depends on how Koha behaves. Do not rely on the contract alone (`EX-001`).
4. **The brief guides the build. It does not script it.**
   - These parts of the brief are limits: part 3 (the API), part 4 (the decisions), part 6 (the behaviors), part 8 (failure and retry), and the packages, projects, and layers in part 7. Stop and ask before you depart from a limit.
   - The approach and the file list in part 7 are guidance. The coder may build the capability a better way, and says how and why under "Deviations" in the pull request. The brief is not edited for that.
   - Inside the limits, the coder decides how to build the capability well, and is expected to.

   Why: a plan that fixes every file and every test in advance leaves the coder little to decide and little reason to question.

### Fix without asking

Fix these without asking, and list each one under "Deviations" in the pull request:

- a bug in the code you are working on;
- a correctness or safety piece that a behavior implies but does not spell out, such as validation, error handling, or cancellation;
- a blocker, such as a build error or a broken reference;
- a better approach or file layout than part 7 describes, inside the projects and layers it lists, with the reason.

Also list there every decision you made about Koha, security, or a standard that the brief does not state, with your evidence. If such a decision is one-way (`CLAUDE.md`, section 3), stop and ask first.

### Stop and escalate

Stop, and open a Level 2 pull request (`CLAUDE.md`, section 3), when the work needs any of these:

- a route, field, error code, or decision that differs from the brief;
- a package, project, or layer that part 7 does not list;
- a departure from a behavior in part 6, or from part 8.

Never build the departure first. Say what you found, what you recommend, and why.

**Missing packages and tools.** If a package or a tool cannot be found or installed, stop and report it. Never substitute one with a similar name.

### One capability at a time

Do not merge behavior from a later capability into `master` until the current capability is `built` and its `ready/` branch exists. Briefs, independent Level 1 fixes, and accepted Level 2 changes that add no unfinished behavior may merge at any time.

Why: every transfer then contains only complete capabilities.

### Pull request size and hand-off

Build a capability in as few pull requests as can each be reviewed well. Normally that is one. The coordinator splits the build only when one pull request would be too large to review in one reading. Each part must still merge with passing checks.

If a session ends with work unfinished, push the branch, and end the draft pull request's description with a "Hand-off" section. It holds the current state, what remains, the decisions made and why, and the next action. A new session must be able to continue from that section and `docs/STATE.md` alone.

## 7. Review

Every pull request is checked by a new session before it merges.

| Check | For | Role |
| --- | --- | --- |
| Brief check | A pull request that writes or revises a brief (section 4) | `reviewer` |
| Review | Every other pull request, including a wording correction to a brief | `reviewer` |
| Verification | Every capability, one time, after the review of its last pull request and before that pull request merges (section 8) | `verifier` |

1. **Each check is independent.** The coordinator starts each check as a new session that did not write the work (`CLAUDE.md`, section 4, rule 2). It gives that session the brief and the code, not the author's explanation. A session that has already checked the work is not new: each round starts another.
2. **The reviewer runs on a different model family from the coders.** If the session that wrote the work ran on the reviewer's family, the pull request says so.

   Why: a model does not notice in a review the design problems that it does not notice when it writes.
3. **A review is a second look, not proof.** The independent evidence is the maintainer's test of the running capability at transfer.
4. **The reviewer answers three questions and reports each rule that the change breaks.** Whether the tests would catch a broken behavior is the verifier's question alone (section 8). The questions, and what counts as blocking, are in `.claude/agents/reviewer.md`. The reviewer does not list a verdict for every rule. A change that works but breaks a rule does not pass. The outcome is recorded under "Review" in the pull request.
5. **What no check has shown is never reported as a pass.** A belief about Koha that only a test double supports is not a pass either: the test Koha must have shown it (`TS-013`). Mark such items "needs maintainer" under "Not proven" in the pull request, so that the maintainer checks them at transfer. A confident guess that is recorded as a pass breaks these rules.

## 8. Verify

Every capability is verified one time: after its code is written and the review of its last pull request found no blocking issue, and before that pull request merges. A brief is never verified. A brief is checked (section 4).

Why: the verification is cheap for what it shows. It breaks each stated behavior in turn and sees whether a test fails. The reviewer does not do this work (section 7, rule 4), so the check has one owner.

### The bar

> Every behavior in part 6 of the brief, and every outcome in part 8, has at least one passing test that fails when that stated behavior is directly broken.

"Directly broken" means the simplest change that makes the stated behavior false: a different status, an input that is no longer rejected, a side effect that no longer happens.

- A behavior without such a test is a **blocking** finding.
- Anything else is **non-blocking** and is recorded for follow-up. Examples: a contrived change that no test catches but that leaves every stated behavior intact, a concern about test quality, unreachable code.

Why the bar is fixed: a deeper search always finds one more change that no test catches.

The verifier also confirms three things about each behavior:

- it **exists**: it is implemented;
- it is **real**: it is not a placeholder, a hard-coded answer, or an empty method;
- it is **wired**: it is registered for dependency injection and reachable from its endpoint (`TS-005`).

The verifier lists the behaviors itself, from the words of the brief. The author prepares no checklist for it. The procedure is in `.claude/agents/verifier.md`.

## 9. Finish

### Setting `built`

1. The coordinator sets `built` when three things hold: the review found no blocking issue, the verification passed, and the checks pass, the test Koha checks included (section 13).
2. To set it, the coordinator adds one commit to the capability's last pull request. The commit sets the brief's status to `built`, updates the capability's entries in `docs/ROADMAP.md` and `docs/STATE.md`, and updates the header date of each file it changes. It changes nothing else. This commit needs no new review.
3. If the maintainer accepted a merge with a test Koha check that could not run, the brief stays `approved`. Once the check has run and passed, the coordinator sets `built` with the same kind of commit, in a pull request of its own. That pull request has a review, and its reviewer runs the test Koha checks.

### The `ready/` branch

After the pull request that sets `built` has merged, the coordinator creates the branch `ready/<NNN>-<short-name>` at the resulting `master` commit. That commit is what the maintainer transfers. A `ready/*` branch is never moved or deleted.

Why a branch and not a tag: the agents' GitHub tools can create branches but not tags.

If a built capability needs a fix afterwards:

1. If behavior from a later capability is already on `master`, stop and ask the maintainer.

   Why: a `ready/` branch cut from `master` would carry that unfinished work.
2. Otherwise, fix it in a normal pull request with its review. A new `verifier` also re-checks the behaviors that the fix touches.
3. Create `ready/<NNN>-<short-name>-2` (then `-3`, and so on) at the `master` commit that merges the fix.

### After the transfer

When the maintainer reports that the capability was merged into production, the coordinator records the report under "Acceptance" in the brief, sets the status to `transferred`, and updates `docs/STATE.md` and `docs/ROADMAP.md`.

**Manual acceptance comes from the maintainer.** It is required for a capability that changes data in Koha. For any other capability the maintainer may skip it, and the skip is recorded. Agents record the maintainer's report word for word. They never fill in results themselves.

## 10. Round limits

A **round** is one pass of the work followed by its check.

| Stage | One round | Limit before the maintainer decides |
| --- | --- | --- |
| **Brief** | The coordinator writes or changes the brief, and a new `reviewer` runs the brief check. | Two rounds per brief, the first included |
| **Review** | A coder writes or fixes one pull request, and a new `reviewer` reviews it. | Three rounds per pull request, or fewer if a round does not reduce the blocking findings |
| **Verification** | A new `verifier` checks the capability. Before a second round, a coder fixes the findings and a new `reviewer` reviews the fix. That review counts as a round of the pull request's review. The second round re-checks only what the fix could affect. | Two rounds per capability |

The limits count the agents' own rounds. A revision that the maintainer asks for when accepting a brief does not count.

**Sending a problem back.** For each blocking finding, the checker says where the problem started: in the brief, or in the code and tests. A finding that started in the brief goes back to the coordinator. If the fix changes an approved brief's decisions, API, behaviors, or packages, the change needs the maintainer's acceptance (Level 2) before the code follows it.

**When a limit is reached.** When the next round would pass a limit, the coordinator stops, records the open findings in the pull request, and labels it `needs-human`. The maintainer decides what comes next.

The limits are starting values. Revisit them once a few capabilities have been built.

## 11. Branches, commits, and pull requests

This section applies to every task that changes a file. Read-only explanations, reviews, and diagnostics need no branch.

### Before you start

1. **Require a clean working tree.** If unrelated or uncommitted work exists, stop and ask the maintainer how to handle it. Do not stash, discard, overwrite, or absorb it.
2. **Check the remote.** `origin` must point to `https://github.com/MinioasisAgent/minioasis-agents`, with or without a `.git` ending.
3. **Fetch `origin/master`.** If it cannot be fetched and verified, stop and report the blocker. Never work from a ref that may be stale.
4. **Create the task branch from `origin/master`.**

When the coordinator starts a second coder to work at the same time as the first, each coder works in its own worktree on its own branch. A branch merges the latest `master` before its pull request merges.

### Branch names

Use `ai/<category>/<three-digit-id>-<short-name>`.

| Category | For | ID |
| --- | --- | --- |
| `sprint` | Writing a brief, or building or verifying its capability | The number of the brief. The short name tells its pull requests apart, for example `ai/sprint/<NNN>-brief` and `ai/sprint/<NNN>-build`. |
| `bug` | Fixing broken existing behavior | The next number in the `bug` series |
| `task` | Every other change | The next number in the `task` series |

Each series continues from the highest number used so far. Count the `ai/*` branches on the remote and the branch names of all pull requests, open or closed. If a name already exists, raise the ID until the name is unique. If the agent platform assigns another branch name, use the `ai/` name anyway. This section is the maintainer's permission to push `ai/*` and `ready/*` branches.

### Commits and pull request titles

Read the complete diff before you commit. Commit only what belongs to the task, in logical commits that can each be reviewed. Start every commit message and every pull request title with a type:

| Type | For |
| --- | --- |
| `brief` | A brief, at any status, on any branch |
| `feat` | Code for behavior that an `approved` brief describes. Never for a `draft` brief. |
| `fix` | Correcting a bug |
| `test` | Adding or changing tests only |
| `refactor` | Restructuring code without changing behavior |
| `docs` | Other documentation, including this rulebook |
| `chore` | Dependencies, configuration, and tooling |
| `ci` | The automated checks |
| `style` | Formatting only |
| `revert` | Undoing an earlier commit |

For work on a brief or its capability, add the brief's number: `brief(<NNN>): write the brief`, `feat(<NNN>): add <capability> endpoint`, `test(<NNN>): cover <case>`. A pull request is squashed into one commit, so its title takes the type that best describes the whole change.

### Pull requests

1. Push only the task branch, and only to the verified `origin`.
2. Open a pull request against `master`, and fill in `.github/pull_request_template.md`.
3. Keep it a draft until it meets the merge conditions of section 12 or, for a Level 2 change, until it is ready for the maintainer.

To revise an existing pull request, add commits to its branch. Do not open a new one.

## 12. Merging

An agent may merge its own Level 1 pull request only when all of these hold:

1. Every required check has completed successfully on the latest commit. The required check is the CI status check `checks`.
2. A new `reviewer` found no blocking issue (section 7). The pull request records the outcome and what ran (`CLAUDE.md`, section 4, rule 6).
3. The pull request is limited to one capability, one fix, or one task.
4. Every test Koha check that applies ran and passed (section 13). If one could not run, the pull request is Level 2, because it would merge with a check left out (`CLAUDE.md`, section 3, item 5). Label it `needs-human`, and say which check and why.

   Why: without this, a capability could again be built on a belief about Koha that nothing has tested.
5. For a capability's last pull request: the verification passed (section 8).
6. `docs/STATE.md` and `docs/ROADMAP.md` are updated where their content changed.

Merge with **squash**, then delete the task branch. The one exception is a production sync, which merges with a merge commit (`docs/transfer.md`, section 4).

A Level 2 pull request waits for the maintainer's acceptance (`CLAUDE.md`, section 3).

## 13. Checks

Before you commit, run every check that applies to the files you changed.

| Change | Check |
| --- | --- |
| C# code or a project file | `scripts/preflight.sh` (`scripts/preflight.ps1` on Windows) |
| Code or a test whose brief marks a behavior for the test Koha | Also the test Koha checks (`TS-013`), run as `docs/running.md` describes. Report "test Koha: passed", "failed", or "not run" with the reason. A failure blocks the merge. A check that could not run makes the pull request Level 2 (section 12). |
| A contract or the generator configuration | Regenerate the affected client (`EX-003`), inspect the generated diff, then run the C# check |
| The solution file only | `dotnet sln minioasis.slnx list` shows every project |
| Dockerfile or Compose | `docker compose config` and `docker compose build`, when Docker is available |
| Documentation or the rulebook | `git diff --check`. Every path, rule ID, and command you mention exists. Each changed document's header is current (section 14). Read the final diff. |

The preflight script runs restore, formatting, the tests, and the publish build. It stops at the first failure. CI runs it on every pull request. While the repository holds no .NET code, it reports that there is nothing to check.

CI holds no Koha credentials and never runs the test Koha checks. Agents run them from a session.

### When a check cannot run or already fails

- **A tool, an SDK, or a network service is unavailable.** Run every check that can run, and report the exact blocker.
- **The check failed before your change.** Confirm that the failure is older than your task and unrelated to it. Do not widen your task to fix it. You may push with the failure disclosed, but nothing merges while a required check fails. Fix the baseline first, as a separate task.
- **Your change caused the failure.** Fix it before the task is complete.

## 14. File headers

Every document starts with two lines:

- **Access:** the level of a change to the file, as `CLAUDE.md`, section 3, decides it. If the line and `CLAUDE.md` ever disagree, `CLAUDE.md` wins and the line is corrected.
- **Last Updated:** the date of the file's last change.

Update `Last Updated` in every document you change. Add both lines to every new document, such as a brief copied from the template. Documents are the Markdown files and the configuration files that allow comments. Code, generated code, contracts, JSON files, and `LICENSE.txt` carry no header.
