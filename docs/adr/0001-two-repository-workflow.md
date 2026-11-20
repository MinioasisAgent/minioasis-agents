- **Access:** Level 2
- **Last Updated:** 2026-11-20

# ADR 0001: Two-repository workflow with autonomous agents

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** Maintainer

## Context

Minioasis is written almost entirely by AI agents. The maintainer has little time. If the maintainer had to approve every task before it started and merge every pull request, routine work would wait on one person, and that person would still have no structured place to review the decisions that actually matter.

The maintainer wants agents to work and merge independently, with human attention reserved for specs and plans, important changes, and a final review before code reaches production.

## Decision

1. **Two repositories.**
   - `MinioasisAgent/minioasis-agents` is the agents' workspace.
   - `GoldBeluga/minioasis` is the production repository. Only the maintainer writes to it, and agents never access it.
   - Code reaches production by transfer, one verified spec at a time, following `docs/workflow/production-transfer.md`.
2. **Two accounts.** Agents act through the `MinioasisAgent` GitHub account. The maintainer acts through their own account, `GoldBeluga`. GitHub can therefore tell an agent's action from the maintainer's.
3. **Three levels of autonomy,** defined in `CLAUDE.md`.
   - **Level 1:** agents merge on their own.
   - **Level 2:** the maintainer accepts the change.
   - **Level 3:** never allowed.
   - The maintainer approves specs and their plans, not individual tasks.
4. **Automated gates replace human review of every pull request in the agents' repository.** An agent merges only when the required checks pass and a review by a different agent finds no blocking issue.
5. **Shared history.** Transfers merge into production with merge commits, at `ready/<NNN>-<name>` branches that agents never move. Production edits return through `sync/production`, also merged with a merge commit.
6. **One spec at a time on `master`,** so that every transfer point contains only complete specs.
7. **All files transfer, including the agents' tooling.** That covers `CLAUDE.md`, `.claude/`, and `docs/`. Keeping both repositories identical preserves the shared history, and specs are useful documentation in production.

## Alternatives considered

| Alternative | Why rejected |
| --- | --- |
| A single repository with human review of every pull request | Makes the maintainer a bottleneck for routine work; contradicts the goal of AI-written code. |
| Fully autonomous agents with no Level 2 | Lets agents change specs, security, dependencies, and their own safeguards without oversight. |
| Agents and the maintainer sharing one GitHub account | GitHub cannot tell who acted, so no review rule can require the maintainer. |
| Squash-merge transfers into production | Breaks the shared history; every later transfer conflicts or repeats old changes. |
| Stripping the agents' tooling from production | The same history problem; loses specs as documentation. |
| Approval through the text of a GitHub comment | Comment text is untrusted input to agents and cannot authorize an action. A merge, an approving review from the maintainer's account, or an in-session `approved` is unambiguous. |
| Agents pushing directly to production | Removes the maintainer's final review, which is the purpose of the second repository. |
| Git tags as transfer points | Agents' GitHub tools can create branches but not tags, so branches are the mechanism agents can use reliably. |
| The maintainer filling in manual acceptance in production | Every transfer would then need a sync back. The maintainer reports results to an agent instead. |

## Consequences

**Positive**

- Routine work flows without waiting for the maintainer.
- Human review concentrates on specs, security, contracts, and acceptance against a real Koha, which agents cannot perform.
- Production holds only complete, verified, reviewed specs.

**Negative and risks**

- **Quality depends on CI and the reviewing agent.** Weak tests can let bad code into the agents' `master`. This is reduced by the best-practice rules that every review checks, by the fixed bar for `verified`, and by the maintainer's test spot-checks at transfer.
- **An agent may misjudge a Level 2 change as Level 1.** This is reduced by the rule "when unsure, Level 2" and by the transfer review. A rule that agents repeatedly misapply should be clarified in `CLAUDE.md`.
- **Level 2 is only partly a technical lock.** Code-owner review makes GitHub require the maintainer's approval on the safeguard files. But the `MinioasisAgent` account owns the workspace repository and could change that setting, and an agent could change a CI workflow inside its own pull request. The backstops are the required status checks on `master` and the "Safeguards" item in the transfer checklist.
- **Working on one spec at a time limits parallel spec work.** This can be revisited, for example with feature flags, if it becomes a bottleneck.
- **Production edits that are not synced back cause conflicts.** This is reduced by preferring fixes in the agents' repository.
