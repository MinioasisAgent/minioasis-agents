- **Access:** Level 2
- **Last Updated:** 2026-11-20

# Git workflow

For every task that changes a file. Read-only explanations, reviews, and diagnostics need no branch. Changing this file is a Level 2 change.

## Before you start

1. **Require a clean working tree.** If unrelated or uncommitted work exists, stop and ask the maintainer how to handle it. Do not stash, discard, overwrite, or absorb it.
2. **Check the remote.** `origin` must point to `https://github.com/MinioasisAgent/minioasis-agents`, with or without a `.git` ending.
3. **Fetch `origin/master`.** If it cannot be fetched and verified, stop and report the blocker. Never work from a ref that may be stale.
4. **Create the task branch from `origin/master`.**

## Branch names

Use `ai/<category>/<three-digit-id>-<short-name>`:

| Category | For | ID |
| --- | --- | --- |
| `sprint` | Drafting, planning, building, or verifying a spec | The spec number. The short name tells its pull requests apart, for example `ai/sprint/<NNN>-draft` and `ai/sprint/<NNN>-token-validation`. |
| `bug` | Fixing broken existing behavior | The next number in the `bug` series |
| `task` | Every other change | The next number in the `task` series |

Each series continues from the highest number used so far, counting the `ai/*` branches on the remote and the branch names of all pull requests, open or closed. If a name already exists, raise the ID until it is unique. If the agent platform assigns another branch name, use the `ai/` name anyway. This section is the maintainer's permission to push `ai/*` and `ready/*` branches.

## Commits and pull request titles

Commit only what belongs to the task, in logical, reviewable commits, after reading the complete diff. Start every commit message and pull request title with a type:

| Type | For |
| --- | --- |
| `spec` | A spec's own documents (`spec.md`, `plan.md`, `verification.md`), at any status, on any branch |
| `feat` | Code for behavior that an `approved` spec describes; never for a `draft` or `reviewed` spec |
| `fix` | Correcting a bug |
| `test` | Adding or changing tests only |
| `refactor` | Restructuring code without changing behavior |
| `docs` | Other documentation, including this rulebook |
| `chore` | Dependencies, configuration, and tooling |
| `ci` | The automated checks |
| `style` | Formatting only |
| `revert` | Undoing an earlier commit |

For spec work, add the spec number: `spec(<NNN>): draft the spec`, `feat(<NNN>): add <capability> endpoint`, `test(<NNN>): cover <case>`. A pull request is squashed into one commit, so its title takes the type that best describes the whole change.

## File headers

Every document starts with two lines:

- **Access:** the level of a change to the file, as `CLAUDE.md` section 5 decides it. If the line and `CLAUDE.md` ever disagree, `CLAUDE.md` wins and the line is corrected.
- **Last Updated:** the date of the file's last change.

Update `Last Updated` in every document you change, and add both lines to every new document, such as a spec copied from a template. Documents are the Markdown files and the configuration files that allow comments. Code, generated code, contracts, JSON files, and `LICENSE.txt` carry no header.

## Pull requests

1. Push only the task branch, and only to the verified `origin`.
2. Open a pull request against `master`, and fill in `.github/pull_request_template.md`.
3. Keep it a draft until it meets the merge conditions below or, for a Level 2 change, is ready for the maintainer.

To revise an existing pull request, add commits to its branch. Do not open a new one.

## Merging a Level 1 pull request

An agent may merge its own Level 1 pull request only when all of these hold:

- Every required check has completed successfully on the latest commit.
- The reviewer found no blocking issue (`docs/workflow/verification.md`), and the pull request records the outcome with the coder's and the reviewer's model and effort.
- The pull request is limited to one spec, one fix, or one task.
- For spec work, the spec's `verification.md` is current, and `docs/STATE.md` and `docs/ROADMAP.md` are updated where their content changed.

Merge with **squash**, then delete the task branch. A Level 2 pull request waits for the maintainer's acceptance (`CLAUDE.md`, section 5).

## `ready/` branches

When a spec is verified, create `ready/<NNN>-<short-name>` at the `master` commit that sets it to `verified`. That commit is what the maintainer transfers. A `ready/*` branch is never moved or deleted.

If a verified spec needs a fix afterwards:

1. If behavior from a later spec is already on `master`, stop and ask the maintainer: a `ready/` branch cut from `master` would carry that unfinished work.
2. Otherwise, fix it in a normal pull request that keeps the spec at the bar for `verified`: update `verification.md`, and have a fresh `verifier` re-check the clauses the fix touches.
3. Create `ready/<NNN>-<short-name>-2` (then `-3`, and so on) at the `master` commit that merges the fix.

## Production sync

When the maintainer pushes production's `master` to `sync/production`:

1. Open a pull request from `sync/production` into `master`.
2. Resolve conflicts in favor of production, unless that would break the checks.
3. Once the checks pass, merge with a **merge commit**, never a squash, to keep the history this repository shares with production.

A sync is Level 1, because the maintainer already decided its behavior. If it touches a safeguard path, it is Level 2 (item 5): label it `needs-human`, since GitHub requires the maintainer's approving review. If it alters behavior that a spec describes, update the spec in a follow-up pull request and note that the change came from production.
