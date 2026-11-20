- **Access:** Level 2
- **Last Updated:** 2026-11-20

# Checks

Which checks to run before committing, and what to do when one cannot run. Changing this file is a Level 2 change.

Run every check that applies to the files you changed.

| Change | Check |
| --- | --- |
| C# code or a project file | `scripts/preflight.sh` (`scripts/preflight.ps1` on Windows) |
| A contract or the generator configuration | Regenerate the affected client (`EX-003`), inspect the generated diff, then run the C# check |
| The solution file only | `dotnet sln minioasis.slnx list` shows every project |
| Dockerfile or Compose | `docker compose config` and `docker compose build`, when Docker is available |
| Documentation or the rulebook | `git diff --check`; every path, rule ID, and command you mention exists; each changed document's header is current (`docs/workflow/git.md`); read the final diff |

The preflight script runs restore, formatting, tests, and the publish build, and stops at the first failure. CI runs it on every pull request. While the repository holds no .NET code, it reports that there is nothing to check.

## When a check cannot run or already fails

- **A tool, SDK, or network service is unavailable:** run every feasible check and report the exact blocker.
- **It failed before your change:** confirm that the failure predates your task and is unrelated to it, and do not widen your task to fix it. You may push with the failure disclosed, but nothing merges while a required check fails: fix the baseline first, as a separate task.
- **Your change caused it:** fix it before the task is complete.
