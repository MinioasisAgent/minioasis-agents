- **Access:** Level 1
- **Last Updated:** 2026-11-23

# Running Minioasis

How to build, test, and run the application on a development machine. Keep this file current when a step changes.

## Prerequisites

- The .NET 10 SDK.
- No Koha server and no credentials. The ordinary tests never need either (`TS-008`).
- For the test Koha checks only (`TS-013`): the five `MINIOASIS_TEST_KOHA_*` variables, which the maintainer sets in the environment's settings. No such check exists yet; the build of the first capability that has one writes here how they are marked and run.

## Run every check

```bash
bash scripts/preflight.sh
```

On Windows, run `pwsh scripts/preflight.ps1`. The script restores, checks formatting, runs the tests, and builds the API for publishing, as `docs/process.md`, section 13, describes.

## Build and test

```bash
dotnet build minioasis.slnx
dotnet test minioasis.slnx
```

The test projects use xUnit v3 through the VSTest runner, which is what `dotnet test minioasis.slnx` starts. They reference `xunit.v3.mtp-off`, the variant of xUnit v3 without Microsoft.Testing.Platform, because the .NET 10 SDK refuses to run a Microsoft.Testing.Platform project through VSTest. A test file needs `using Xunit;`.

Each test run prints a warning that `VSTEST_ARTIFACTS_DIRECTORY` was not found and that `Microsoft.NET.Test.Sdk` may be outdated. The package is the latest version; the warning comes from the SDK's bundled test platform and is harmless. Do not downgrade to silence it.

## Run the API

```bash
dotnet run --project Minioasis.Api
```

The API has no endpoints yet: every request is answered with 404.

## Generate a Koha client

Not used yet. The exact command is added here when the first client is generated (`EX-003`).
