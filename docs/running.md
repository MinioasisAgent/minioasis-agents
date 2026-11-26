- **Access:** Level 1
- **Last Updated:** 2026-11-26

# Running Minioasis

How to build, test, and run the application on a development machine. Keep this file current when a step changes.

## Prerequisites

- The .NET 10 SDK.
- No Koha server and no credentials. The ordinary tests never need either (`TS-008`).
- For the test Koha checks only (`TS-013`): the five `MINIOASIS_TEST_KOHA_*` variables, which the maintainer sets in the environment's settings. See "Run the test Koha checks" below.

## Run every check

```bash
bash scripts/preflight.sh
```

On Windows, run `pwsh scripts/preflight.ps1`. The script restores, checks formatting, runs the tests, and builds the API for publishing, as `docs/process.md`, section 13, describes.

## Build and test

The backend is in `backend/`. Run every `dotnet` command in this file from that folder.

```bash
dotnet build minioasis.slnx
dotnet test minioasis.slnx
```

The test projects use xUnit v3 through the VSTest runner, which is what `dotnet test minioasis.slnx` starts. They reference `xunit.v3.mtp-off`, the variant of xUnit v3 without Microsoft.Testing.Platform, because the .NET 10 SDK refuses to run a Microsoft.Testing.Platform project through VSTest. A test file needs `using Xunit;`.

Each test run prints a warning that `VSTEST_ARTIFACTS_DIRECTORY` was not found and that `Microsoft.NET.Test.Sdk` may be outdated. The package is the latest version; the warning comes from the SDK's bundled test platform and is harmless. Do not downgrade to silence it.

## Run the API

The API needs the three Koha settings of brief 001, part 3, and the three Cloudflare Access settings of brief 002, part 3. It refuses to start when one is missing, blank, or malformed, and the message names the setting and never its value.

```bash
export MINIOASIS_KOHA_BASE_URL='https://koha.example.org'
export MINIOASIS_KOHA_CLIENT_ID='…'
export MINIOASIS_KOHA_CLIENT_SECRET='…'
export MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN='https://<team>.cloudflareaccess.com'
export MINIOASIS_CLOUDFLARE_ACCESS_AUDIENCE='…'
export MINIOASIS_CLOUDFLARE_ACCESS_EMAIL='…'
dotnet run --project Minioasis.Api
```

The Koha base URL has no path; Minioasis adds `/api/v1`. `http` is accepted only for `localhost`, `127.0.0.1`, and `[::1]`. The first token is requested on the first request, so the API starts even when Koha is down. The application sends no Cloudflare Access headers, so it cannot reach the test Koha directly.

| Setting | Value |
| --- | --- |
| `MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN` | The team's address at Cloudflare Access: an absolute `https` URL without a user name, password, path, query, or fragment. A trailing `/` and a port are accepted. `http` is never accepted. |
| `MINIOASIS_CLOUDFLARE_ACCESS_AUDIENCE` | The audience tag that Cloudflare shows on the page of the Access application. |
| `MINIOASIS_CLOUDFLARE_ACCESS_EMAIL` | The one email address that may use Minioasis. Set it only where the application runs; it is never written in the repository. |

The signing keys are fetched from `<team domain>/cdn-cgi/access/certs` on the first request, not at startup, so the API starts even when Cloudflare cannot be reached. Until the keys can be fetched, every request is answered 503.

## Call the API by hand

Every request, on every path, needs two headers. Without them the API answers before routing, so even an unknown path gets 401 or 403, not 404.

| Header | Value | Without it |
| --- | --- | --- |
| `Cf-Access-Jwt-Assertion` | A token that Cloudflare Access issued for the application of `MINIOASIS_CLOUDFLARE_ACCESS_AUDIENCE` to the email of `MINIOASIS_CLOUDFLARE_ACCESS_EMAIL` | 401 `unauthenticated` |
| `X-Requested-With` | `XMLHttpRequest`, exactly | 403 `request_refused` |

A token for another email, or one without an email, such as a service token's, gets 403 `access_denied`.

The application has no development mode, and the stand-in for Cloudflare Access that the joint check uses is not in the repository (`docs/process.md`, section 8). So a token comes only from the real Cloudflare setup: sign in to the Access application in a browser, and copy the value of its cookie `CF_Authorization`, which holds the application token, from the browser's developer tools. The token expires; after that, sign in again and copy the new value.

```bash
export TOKEN='…'
curl -i -H "Cf-Access-Jwt-Assertion: $TOKEN" -H 'X-Requested-With: XMLHttpRequest' \
  http://localhost:5000/api/v1/items/12
```

The port is the one that `dotnet run` prints. The API has one route, `GET /api/v1/items/{id}`.

## Run the test Koha checks

The test Koha checks (`TS-013`) are in `backend/Minioasis.Koha.Tests/TestKoha/`, for brief 001, and in `backend/Minioasis.Api.Tests/TestKoha/`, for brief 002. Each carries the xUnit trait `Category=TestKoha`. Run them alone with:

```bash
dotnet test minioasis.slnx --filter "Category=TestKoha"
```

They read the five `MINIOASIS_TEST_KOHA_*` variables. `MINIOASIS_TEST_KOHA_BASE_URL` has the format of `MINIOASIS_KOHA_BASE_URL`, with `https://`. When any of the five is missing or blank, each check reports itself as skipped, so `scripts/preflight.sh` and CI pass without them. When all five are set, `dotnet test minioasis.slnx` and `scripts/preflight.sh` run them too. A skipped check is "test Koha: not run", never a pass.

The checks add the two Cloudflare Access headers through a test-only handler. The checks for behaviors 1 and 5 create a bibliographic record with one item, and delete both. The barcode starts with `MO` and is unique per run.

The check for behavior 16 of brief 002 asks the test Koha's address with the Cloudflare Access service token, and takes the application token that Cloudflare returns in the cookie `CF_Authorization`. It reads the team domain and the audience from that token, starts the API with them, and lets the API fetch the real signing keys from the team's `/cdn-cgi/access/certs`. It needs the session's network to reach the team's address at Cloudflare. It prints no value that it reads.

## Generate a Koha client

The clients are generated by NSwag 14.7.1, pinned in `backend/dotnet-tools.json`, from the focused contracts in `contracts/` (`EX-002`, `EX-003`). `backend/nswag.json` takes three variables. Regenerate only the client whose contract or configuration changed, inspect the generated diff, and commit the contract, the configuration, and the generated files together.

```bash
dotnet tool restore
dotnet nswag run nswag.json /variables:Contract=oauth,Feature=OAuth,FeatureSingular=OAuth
dotnet nswag run nswag.json /variables:Contract=items,Feature=Items,FeatureSingular=Item
```

| Contract | Output |
| --- | --- |
| `contracts/oauth.openapi.json` | `backend/Minioasis.Koha/Generated/OAuth/` |
| `contracts/items.openapi.json` | `backend/Minioasis.Koha/Generated/Items/` |

`nswag.json` excludes the header parameter `x-koha-embed`: NSwag 14.7.1 generates code for it that does not compile, and Minioasis does not send it.
