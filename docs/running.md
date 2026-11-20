- **Access:** Level 1
- **Last Updated:** 2026-11-20

# Running Minioasis

How to build, test, and run the application on a development machine. Keep this file current when a step changes.

## Prerequisites

- The .NET 10 SDK.
- No Koha server and no credentials. Tests never need either (`TS-008`).

## Run every check

```bash
bash scripts/preflight.sh
```

On Windows, run `scripts/preflight.ps1`. The script restores, checks formatting, runs the tests, and builds the API for publishing, as `docs/workflow/checks.md` describes.

## Build and test

```bash
dotnet build minioasis.slnx
dotnet test minioasis.slnx
```

The test projects use xUnit v3 through the VSTest runner, which is what `dotnet test minioasis.slnx` starts. Each test project sets `IsTestingPlatformApplication` to `false` for that reason.

## Run the API

```bash
dotnet run --project Minioasis.Api
```

The API has no endpoints yet: every request is answered with 404.

## Generate a Koha client

Not used yet. The exact command is added here when the first client is generated (`EX-003`).
