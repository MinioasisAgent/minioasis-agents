- **Access:** Level 2
- **Last Updated:** 2026-11-25

# Transfer to production

How a built capability moves from `MinioasisAgent/minioasis-agents` (the agents' repository) to `GoldBeluga/minioasis` (production), and how an edit made in production comes back. Changing this file is a Level 2 change.

This file is for the maintainer. Agents never run its commands and never access the production repository (`CLAUDE.md`, section 3, Level 3). The one part for agents is "What the agent does" in section 4.

Work moves one way, one built capability at a time. Both repositories share one commit history. Because of that, each transfer shows only the new work.

Every file transfers, including `CLAUDE.md`, `.claude/`, and `docs/`.

Why: a transfer merges a branch, and the branch holds every file. Leaving files out would break the shared history.

## 1. Rules

1. **Transfer only at a `ready/<NNN>-<short-name>` branch.** Agents create it when a capability is `built`, and never move it (`docs/process.md`, section 9). Never transfer the agents' `master`.

   Why: `master` may hold unfinished work.
2. **Merge every transfer with "Create a merge commit".**

   Why: a squash or a rebase breaks the shared history, and every later transfer then conflicts.
3. **Prefer fixes in the agents' repository.** If your review finds a problem, ask an agent to fix it. The agent creates a branch such as `ready/<NNN>-<short-name>-2`, and you transfer that branch. Edit production only for an urgent or trivial fix, and sync the edit back (section 4).

## 2. One-time setup

### In `MinioasisAgent/minioasis-agents`

1. Add `GoldBeluga` as a collaborator with write access (**Settings → Collaborators**). You need it to review pull requests, to be a code owner, and to push `sync/production`.
2. Create the label `needs-human` (**Issues → Labels → New label**). Agents put it on the pull requests that wait for you.
3. Apply the branch protection of section 5.

### In Cloudflare Access

The Access application for the staff address is the first of the two boundaries (`FE-004`). Set it up so that:

1. It covers the whole address, with no path left out and no policy that lets a request through without a sign-in. The frontend's files are protected like the API.
2. Google is its only sign-in method.
3. Its policy admits the one staff email, the same one that the backend is configured with (`docs/briefs/002-staff-access-control.md`, part 3).

### For production

Create an empty private repository `GoldBeluga/minioasis`. Give neither the Claude GitHub App nor `MinioasisAgent` access to it. Then, on your computer:

```bash
git clone https://github.com/GoldBeluga/minioasis.git
cd minioasis
git remote add agents https://github.com/MinioasisAgent/minioasis-agents.git
git remote set-url --push agents DISABLED
```

The push URL `DISABLED` stops an accidental push to the agents' repository. The sync in section 4 pushes with an explicit URL.

### First transfer

Production starts empty, so the first transfer creates its `master`. First review the code on GitHub, in the agents' repository, with the checklist of section 3. Then:

```bash
git fetch agents
git switch --no-track -c master agents/ready/<NNN>-<short-name>
git push -u origin master
```

Then apply production's branch protection (section 5), and tell an agent that the capability was transferred.

## 3. Routine transfer

1. Find the capability's `ready/` branch in the agents' repository. It is `ready/<NNN>-<short-name>`. If a fix followed, take the one with the highest ending: `-2`, `-3`, and so on.
2. Create a review branch from it:

   ```bash
   git switch master
   git pull
   git fetch agents
   git switch --no-track -c review/<NNN>-<short-name> agents/ready/<NNN>-<short-name>
   git push -u origin review/<NNN>-<short-name>
   ```

3. Open a pull request in `GoldBeluga/minioasis` from `review/<NNN>-<short-name>` into `master`. Review it with the checklist below.
4. Merge with **Create a merge commit**.
5. Tell an agent that the capability was transferred. Give the results of your manual acceptance, or say that you skipped it. The agent records your report in the brief, and updates the brief's status and `docs/STATE.md`. So you never edit files in production for this.

### Review checklist

- [ ] **Pull requests:** in the agents' repository, the capability's pull requests hold nothing under "Deviations" or "Not proven" that you did not expect. Every "needs maintainer" item is one that you will check below.
- [ ] **Manual acceptance:** run the capability against the test Koha, or against another Koha of yours that holds no production data. Follow part 2 of `docs/briefs/<NNN>-<short-name>.md`, and note what you observe. This is required for a capability that changes data in Koha, and optional for one that only reads.
- [ ] **Test spot-check:** pick one or two behaviors from part 6 of the brief, and read the tests that cover them. If you have time, break one line of the code and confirm that a test fails.
- [ ] **Packages:** the package references in the `*.csproj` files and in the .NET tool manifest are the ones that part 7 of the brief lists.
- [ ] **Security:** authentication, logging, configuration, and any container or deployment files. No secret and no patron data appears in a log.

  Why: an agent merges security code that builds exactly what an approved brief states. So this item is the last check of that code before production.
- [ ] **Public contract:** the routes, and `backend/Minioasis.Api/Resources/errors.en.json`.
- [ ] **Safeguards:** every change to a path in `.github/CODEOWNERS` is one that you accepted. GitHub enforces this only in part, so this item is the backstop.
- [ ] **CI:** passing on the pull request.

You need not review style, naming, or layering. The preflight script and the reviewer check them against `docs/best-practices/`.

## 4. Syncing production edits back

This is needed only after a commit that was made directly in production.

### What the maintainer does

```bash
git switch master
git pull
git push https://github.com/MinioasisAgent/minioasis-agents.git master:sync/production
```

Then ask an agent to merge `sync/production`. If your edit touched a path in `.github/CODEOWNERS`, approve the agent's pull request.

### What the agent does

1. Open a pull request from `sync/production` into `master`.
2. Resolve conflicts in favor of production, unless that would break the checks.
3. Once the checks pass, merge with a **merge commit**, never a squash.

   Why: a squash would break the history that this repository shares with production.

A sync is Level 1, because the maintainer already decided its content. If it touches a path in `.github/CODEOWNERS`, it is Level 2 (`CLAUDE.md`, section 3, item 5). Label it `needs-human`, because GitHub requires the maintainer's approving review.

If the sync alters behavior that a brief describes, update the brief in a follow-up pull request, and note that the change came from production.

## 5. Branch protection

Set these under **Settings → Rules → Rulesets**, or under **Branches → Branch protection rules**.

### `minioasis-agents`: `master`

- Require a pull request before merging, with **0** required approvals.

  Why: agents merge their own Level 1 work, and GitHub does not let an author approve their own pull request.
- Turn on **Require review from Code Owners**. A pull request that changes a path in `.github/CODEOWNERS` then cannot merge without your approving review.
- Require the status check `checks` from `.github/workflows/checks.yml`.
- Block force pushes and branch deletion.
- Allow squash merging and merge commits. Do not require linear history.

  Why: `sync/production` needs a merge commit.
- **Allow no bypass.** Leave the ruleset's bypass list empty. In a classic rule, tick **Do not allow bypassing the above settings**.

  Why: `MinioasisAgent` owns the repository. Without this setting it can merge past every rule above.

`MinioasisAgent` can still change these settings. They catch mistakes. They are not a lock.

### `minioasis-agents`: `ready/*`

- Block force pushes and branch deletion, so that a transfer point cannot change.

### `minioasis`: `master`

GitHub's free plan protects branches only in public repositories. For a private repository on the free plan, you can upgrade, make the repository public, or go without protection. Without protection, always merge through a pull request, with a merge commit.

- Require a pull request before merging, with **0** required approvals. You are the only reviewer, and your merge is your approval.
- Require the same status check. The workflow is transferred with the code.
- Block force pushes and branch deletion.
- Allow merge commits only: disable squash merging and rebase merging. Do not require linear history.

## 6. Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| The transfer shows changes that were already transferred. | An earlier transfer was squashed or rebased. | Ask an agent to diagnose. Usually one merge of production's `master` through `sync/production` fixes it. |
| The transfer has merge conflicts. | Production edits were not synced back. | Sync them back (section 4). Wait for the agents to merge them and to create a new `ready/` branch. Then transfer that branch. |
| There is no `ready/` branch. | The capability is not `built` yet. | Check the `Status:` line of its brief. Transfer only built capabilities. |
| GitHub reports an error in `.github/CODEOWNERS`. | The owner has no write access. | Add `GoldBeluga` as a collaborator with write access. |
