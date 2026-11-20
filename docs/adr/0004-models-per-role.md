- **Access:** Level 2
- **Last Updated:** 2026-11-20

# ADR 0004: Models per role

- **Status:** Accepted
- **Date:** 2026-11-19
- **Deciders:** Maintainer

## Context

Minioasis is written by AI agents, and the maintainer reviews little of the code directly. The quality of the code therefore depends on which model does which work, how much effort it spends, and whether the check of a change can see what its author missed.

Three model families are available. Fable is the most capable and the most expensive. Opus is the vendor's default general-purpose model. Sonnet is the cheapest of the three and is built for speed. At the time of writing, Fable costs about twice as much per token as Opus, and Opus about twice as much as Sonnet.

Each model also takes an effort level: `low`, `medium`, `high`, `xhigh`, or `max`. A higher level means more thorough work and more tokens.

## Decision

For now, every role runs on Opus. A role's effort follows how important the role is, not how hard its individual tasks are. That effort is the role's default: the coordinator may raise or lower it for a specific task that needs it, and says why in the pull request. No role ever runs above `xhigh`.

The roles are listed in `CLAUDE.md`, section 10. Each role except the coordinator has a file in `.claude/agents/` that sets its model and default effort, so the tool applies them and no agent has to remember them. The maintainer sets the coordinator's when starting a session.

| Role | Model | Default effort | Reason |
| --- | --- | --- | --- |
| Coordinator | Opus | `high` | Its mistakes are the most expensive: a misread request or a wrongly judged level sends every other role in the wrong direction. |
| Coder (heavy) | Opus | `high` | It writes all application behavior and its tests. |
| Coder (light) | Opus | `medium` | It makes only changes the coordinator has specified exactly, with no change in behavior. |
| Reviewer | Opus | `high` | Every agent merge depends on the reviewer finding what the coder missed. |
| Verifier | Opus | `high` | Verification is the last check before a spec can go to production. |

Role files name the family only, so each role runs on the latest version. Recent versions have cost less than the ones they replaced, and old versions are retired in time; pinning a version would mean editing the rulebook at every release.

## Alternatives considered

| Alternative | Why not, for now |
| --- | --- |
| Reviewer and verifier on Fable | A different and stronger model would see more of what the coder missed, at about twice the cost per token of Opus. It is the first change to consider (see "Consequences"). |
| Effort set by how hard each role's usual task is | The maintainer chose to set it by the role's importance, and to adjust it for a task that needs more or less. |
| Pinned model versions | See the paragraph after the table above. |

## Consequences

- A change is checked by a fresh session of the model that wrote it, not by a different model. A fresh session does not share the author's reasoning, but it can share the model's blind spots: design problems that a model does not notice when writing, it may not notice when reviewing. "Independence" in `docs/workflow/verification.md` says where the independent evidence comes from.
- If problems that review or verification should have caught reach the transfer, move the reviewer and the verifier to a different family, such as Fable, before changing anything else about them.
- If the coordinator misjudges levels or misreads the maintainer, raise its effort to `xhigh` before changing anything else.
- Every effort that differs from a role's default is recorded in the pull request with its reason. If a role keeps needing more or less, change its default.
- "Latest" means behavior can change without a change in this repository. A new version may be tuned differently, and the same effort word may mean a little more or less work. The pull request records the model and effort that ran, so a change in quality can be traced.
- The role files are under `.claude/`, so changing a model or a default effort is a Level 2 change. Raising or lowering the effort for one task is not. Revisit them when a new family appears or prices change markedly.
