- **Access:** Level 2
- **Last Updated:** 2026-11-20

# Production transfer runbook

For the maintainer. Agents never run it and never access the production repository. Changing this file is a Level 2 change.

Work moves one way, from `MinioasisAgent/minioasis-agents` (the agents' repository) to `GoldBeluga/minioasis` (production), one verified spec at a time. Both repositories share one commit history, which is what makes each transfer show only the new work.

## Rules

1. **Transfer only at a `ready/<NNN>-<name>` branch.** Agents create it when a spec is verified and never move it. Never transfer the agents' `master`, which may hold unfinished work.
2. **Merge every transfer with "Create a merge commit".** A squash or a rebase breaks the shared history, and every later transfer then conflicts.
3. **Prefer fixes in the agents' repository.** If review finds a problem, ask an agent to fix it; it creates a branch such as `ready/<NNN>-<name>-2`, which you transfer instead. Edit production only for an urgent or trivial fix, and sync the edit back.

## One-time setup

**In `MinioasisAgent/minioasis-agents`:**

1. Add `GoldBeluga` as a collaborator with write access (**Settings → Collaborators**). You need it to review pull requests, to be a code owner, and to push `sync/production`.
2. Create the label `needs-human` (**Issues → Labels → New label**). Agents put it on pull requests that wait for you.
3. Apply the branch protection below.

**For production:** create an empty private repository `GoldBeluga/minioasis`, and give neither the Claude GitHub App nor `MinioasisAgent` access to it. Then, on your computer:

```bash
git clone https://github.com/GoldBeluga/minioasis.git
cd minioasis
git remote add agents https://github.com/MinioasisAgent/minioasis-agents.git
git remote set-url --push agents DISABLED
```

The push URL `DISABLED` stops accidental pushes to the agents' repository; the sync step below pushes with an explicit URL instead.

### First transfer

Production starts empty, so the first transfer creates its `master`. First review the code on GitHub in the agents' repository, with the checklist below.

```bash
git fetch agents
git switch --no-track -c master agents/ready/<NNN>-<name>
git push -u origin master
```

Then apply production's branch protection, and tell an agent that the spec was transferred.

## Routine transfer

1. Find the spec's `ready/` branch in the agents' `docs/STATE.md`.
2. Create a review branch from it:

   ```bash
   git switch master
   git pull
   git fetch agents
   git switch --no-track -c review/<NNN>-<name> agents/ready/<NNN>-<name>
   git push -u origin review/<NNN>-<name>
   ```

3. Open a pull request in `GoldBeluga/minioasis` from `review/<NNN>-<name>` into `master`, and review it with the checklist below.
4. Merge with **Create a merge commit**.
5. Tell an agent that the spec was transferred, with your manual acceptance results, or that you skipped them. The agent records them in the spec and updates its status and `docs/STATE.md`, so you never edit files in production for this.

### Review checklist

- [ ] **Spec:** the behavior matches the approved `docs/specs/<NNN>-<name>/spec.md`.
- [ ] **Verification:** in `verification.md`, every clause has a proving test, every requirement row passes, and the "Proves" column reads correctly.
- [ ] **Test spot-check:** one or two test bodies assert what their row claims. If you have time, break one line of the code and confirm that a test fails.
- [ ] **Manual acceptance:** run the scenarios in `verification.md` against a test Koha and note what you observe. Required for a spec that writes to Koha; optional for one that only reads.
- [ ] **Files:** nothing outside what the spec's `plan.md` lists.
- [ ] **Security:** authentication, logging, configuration, and any container or deployment files; no secrets or patron data in logs.
- [ ] **Public contract:** routes and `Minioasis.Api/Resources/errors.en.json`.
- [ ] **Dependencies:** package references in `*.csproj` files, and the .NET tool manifest.
- [ ] **Safeguards:** every change to a path in `.github/CODEOWNERS` is one you accepted. GitHub enforces this only in part, so this item is the backstop.
- [ ] **CI:** passing on the pull request.

Style, naming, and layering are checked by the preflight script and the reviewer against `docs/best-practices/`; you need not review them.

## Syncing production edits back

Only needed after a commit made directly in production:

```bash
git switch master
git pull
git push https://github.com/MinioasisAgent/minioasis-agents.git master:sync/production
```

Then ask an agent to merge `sync/production`; it uses a merge commit. If your edit touched a path in `.github/CODEOWNERS`, approve its pull request.

## Branch protection

Under **Settings → Rules → Rulesets**, or **Branches → Branch protection rules**.

### `minioasis-agents`: `master`

- Require a pull request before merging, with **0** required approvals: agents merge their own Level 1 work, and GitHub does not let an author approve their own pull request.
- Turn on **Require review from Code Owners**, so a pull request that changes a path in `.github/CODEOWNERS` cannot merge without your approving review.
- Require the status check `checks` from `.github/workflows/checks.yml`.
- Block force pushes and branch deletion.
- Allow squash merging and merge commits; merge commits are needed for `sync/production`. Do not require linear history.
- **Allow no bypass:** leave the ruleset's bypass list empty or, in a classic rule, tick **Do not allow bypassing the above settings**. `MinioasisAgent` owns the repository, so without this it can merge past every rule above.

`MinioasisAgent` can still change these settings. They catch mistakes; they are not a lock.

### `minioasis-agents`: `ready/*`

- Block force pushes and branch deletion, so a transfer point cannot change.

### `minioasis`: `master`

GitHub's free plan protects branches only in public repositories. For a private repository on the free plan, upgrade, make it public, or go without protection and always merge through a pull request with a merge commit.

- Require a pull request before merging, with **0** required approvals: you are the only reviewer, and your merge is your approval.
- Require the same status check; the workflow is transferred with the code.
- Block force pushes and branch deletion.
- Allow merge commits only: disable squash and rebase merging. Do not require linear history.

## Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| The transfer shows changes that were already transferred | An earlier transfer was squashed or rebased | Ask an agent to diagnose. Usually a one-off merge of production's `master` through `sync/production` fixes it. |
| Merge conflicts in the transfer | Production edits were not synced back | Sync them back, wait for the agents to merge them and create a new `ready/` branch, then transfer that. |
| No `ready/` branch | The spec is not verified yet | Check `docs/STATE.md`. Transfer only verified specs. |
| GitHub reports an error in `.github/CODEOWNERS` | The owner has no write access | Add `GoldBeluga` as a collaborator with write access. |
