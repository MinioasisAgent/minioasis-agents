- **Access:** Level 1
- **Last Updated:** 2026-11-23

# 001 — Get item

- **Status:** draft
- **Changes data in Koha:** no
- **Verification:** not required
- **Related:** none

This brief also builds the two things every later capability shares: the connection to Koha (token, retry, deadline) and the API error contract. The maintainer chose to put the three in one brief (part 4). Later briefs refer to this one for both.

## 1. Goal

A staff client can ask Minioasis for one Koha item by its Koha item number and get its identifiers, barcode, and call number. When anything fails, the client gets one predictable error shape, and the real cause is in the server log.

## 2. What people do and see

1. The client sends `GET /api/v1/items/12`.
2. Minioasis gets a Koha access token if it has none, asks Koha for item 12, and answers 200 with the four fields of part 3.
3. If the number is not a positive whole number, the client sees 400 "Invalid item identifier", and Koha is not called.
4. If Koha has no such item, the client sees 404 "Item not found", with the number in the detail.
5. If Minioasis cannot sign in to Koha, Koha is down or slow, or Koha answers with something unusable, the client sees "Internal error" with a `traceId`. The log line with the same `traceId` holds the cause.

## 3. The API

| Method | Route | Success | Request | Response fields and types |
| --- | --- | --- | --- | --- |
| GET | `/api/v1/items/{id}` | 200, `application/json` | `id`: the Koha item number, as a path segment | `id` (string), `bibliographicRecordId` (string), `barcode` (string or null), `callNumber` (string or null) |

- `id` is Koha's `item_id` and `bibliographicRecordId` is Koha's `biblio_id`, each written as a decimal string. `barcode` is Koha's `external_id`. `callNumber` is Koha's `callnumber`.
- A valid `id` is one or more ASCII digits, without a leading zero, whose value is from 1 to 2147483647. Whitespace, a sign, a decimal point, or any other character makes it invalid.
- `GET /api/v1/items/` has no `id` segment, so it matches no route and returns 404 `not_found`. An `id` of only a space, `%20`, does match the route and returns 400.

Every error is Problem Details (`ER-005`): content type `application/problem+json`, with `status`, `title`, `detail` (except where masked), `code`, and `traceId`, and without `type` and `instance`. `traceId` is an opaque string.

| Situation | HTTP status | Public code | Title / detail shown to staff |
| --- | --- | --- | --- |
| Invalid `id` | 400 | `items.invalid_identifier` | "Invalid item identifier" / "The item identifier must be a valid positive identifier." |
| Koha has no such item | 404 | `items.not_found` | "Item not found" / "No item was found with identifier {id}." |
| No route matches the path | 404 | `not_found` | "Not found" / "The requested resource does not exist." |
| The route exists, but not for this method | 405 | `method_not_allowed` | "Method not allowed" / "This resource does not support the request method." The `Allow` header that the framework sets is kept (RFC 9110, section 15.5.6). |
| Minioasis cannot authenticate to Koha | 500 | `internal_error` | "Internal error", no detail |
| Koha's answer cannot be used | 502 | `internal_error` | "Internal error", no detail |
| Koha is unavailable | 503 | `internal_error` | "Internal error", no detail |
| Unexpected failure | 500 | `internal_error` | "Internal error", no detail |

**Internal codes, logged only:** `koha.authentication_failed`, `koha.invalid_response`, `koha.unavailable`, `system.unexpected`. The three `koha.*` codes are shared: every later capability that calls Koha uses the same three. The log line also names the operation, here `items.get`, so the code says what failed and the operation says where.

**How Koha's answers are translated** (`ER-010`), for `GET /items/{item_id}`:

| Koha's answer | Internal outcome (never shown to the client) | Status the client gets |
| --- | --- | --- |
| 200 with `item_id` and `biblio_id` both positive | Found | 200 |
| 404 whose JSON body has `error_code` equal to `not_found` | Not found | 404 |
| A token cannot be obtained, for any reason of `EX-013`; 401 again after the one retry; 403 | Authentication failed | 500 |
| 429; any 5xx; a connection failure; a response that is cut off; the deadline expires while the item is requested | Unavailable | 503 |
| Any 3xx. Redirects are not followed. | Invalid response | 502 |
| Any other status, including a 404 without that `error_code`; a 200 whose body cannot be read; a 200 without a positive `item_id` and `biblio_id` | Invalid response | 502 |

**Configuration** (`EX-005`), read through ASP.NET configuration:

| Name | Value |
| --- | --- |
| `MINIOASIS_KOHA_BASE_URL` | The address of Koha: an absolute `https` URL without a path, query, or fragment, for example `https://koha.example.org`. `http` is accepted only when the host is exactly `localhost`, `127.0.0.1`, or `[::1]`. A user name or password in the URL is refused. A trailing `/` is accepted. Minioasis adds `/api/v1` itself. |
| `MINIOASIS_KOHA_CLIENT_ID` | The client identifier of the Koha service account. |
| `MINIOASIS_KOHA_CLIENT_SECRET` | Its secret. |

If any of the three is missing, blank, or malformed, the application refuses to start. The message names the setting and never shows a value.

**Koha operations**

| Operation | Read or write | Focused contract |
| --- | --- | --- |
| `POST /oauth/token` (`tokenOAuth`), client-credentials grant | neither | `contracts/oauth.openapi.json` |
| `GET /items/{item_id}` (`getItem`) | read | `contracts/items.openapi.json` |

**Security**

- **Who may call it: anyone who can reach the API.** Staff sign-in does not exist yet, and this brief sets no listening address. The maintainer's design is that the API listens on localhost only and validates the Cloudflare Access token that the user interface forwards; that is the next brief (part 5).
- Get item works only if the Koha service account has the `catalogue` permission, as the contract states for `getItem`.
- No patron data is read, returned, or logged.
- The log may hold the trace identifier, internal error codes, HTTP status codes, the `error` word of a Koha token response (`access_denied` for a wrong secret and `unauthorized_client` for an unknown client identifier, on the test Koha, 26.05), logged only when it is at most 64 characters, and the Koha client identifier. It never holds the client secret, an access token, or an `Authorization` header (`ER-008`).

## 4. Decisions

**Made by the maintainer** (all on 2026-11-23)

| Decision | Reason |
| --- | --- |
| One brief covers the Koha connection, the API error contract, and get item. This is an intended exception to `docs/process.md`, section 3, rule 4. | One approval and one build; get item gives the other two a real caller. |
| Look up by Koha item number only. | The smallest slice that proves the whole path. Barcode lookup is a later route. |
| The response is the four fields of part 3, with identifiers as strings; the two `items.*` codes and their text. | Kept from the first attempt; nothing was found wrong with them. |
| The first token is requested on the first Koha call, not at startup. | The application still starts when Koha is down. |
| Bad settings stop the application at startup. | A wrong setting shows at once, not on the first request. |
| The base URL holds no path; Minioasis adds `/api/v1`. | One less thing to get wrong in a setting. |
| Production reaches Koha directly. The application sends no Cloudflare Access headers. | The maintainer's deployment. |
| The deadline for all Koha work on one request is 10 seconds. | "10s looks sufficient." |
| Server failures are masked as `ER-006` says. A 403 from Koha is an authentication failure. | The service account lacks a permission; staff cannot fix it. |
| An unknown route and a wrong method answer in the same Problem Details shape, with the codes `not_found` and `method_not_allowed`. | Clients parse one shape everywhere. |
| The three Koha failure codes are generic (`koha.*`), not one set per capability. | The cause belongs to the connection, not to the capability, and one set is reused everywhere. |
| The Koha client identifier may be logged. | It is an identifier, not a secret, and it helps find a wrong setting. |
| The test Koha check creates its own record and item, and deletes them. | `TS-013`. |
| Verification is not required, although the capability handles credentials (`docs/process.md`, section 8). | Nothing changes data in Koha; the tokens are easy to rotate. |
| The route ships open until brief 002 is built. | "Let's not focus on this first." |

**Made by the agent**

| Decision | Alternative rejected | Reason and evidence |
| --- | --- | --- |
| The client identifier and secret go to the token endpoint in the HTTP Basic header. The header value is Base64 of `identifier:secret` in UTF-8, as curl builds it. The form body carries `grant_type=client_credentials` and never the secret. | Both in the form body. | RFC 6749, section 2.3.1, prefers Basic. Test Koha, 26.05, 2026-11-23: Basic alone returned 200 with `token_type` Bearer and `expires_in` 3600; Basic wins over a wrong secret in the body. The contract marks `client_id` and `client_secret` optional. The client that NSwag 14.7.1 generates sends them as empty fields when they are not set; the test Koha returned 200 for Basic with both fields empty. The Basic header is set on the token HTTP client (`EX-007`). |
| A token is reused while more than 60 seconds of its lifetime remain. | No margin. | A token that expires in flight costs a 401 and a retry. With Koha's 3600 seconds, 60 costs nothing. Two-way. |
| A token request is never retried. | One retry. | `EX-010`: only a GET is retried. The next request tries again anyway. |
| A wrong secret is an authentication failure, like every other token failure. | A separate "wrong credentials" outcome. | `EX-013`. Test Koha, 26.05: a wrong secret returns 403 `{"error":"access_denied"}`. The log shows the status and that word. |
| An invalid `id` is rejected by Minioasis before Koha is called, and `0` is invalid. | Pass every value to Koha. | Test Koha, 26.05: `abc` returns 400 with an `errors` array, and `0` and `-1` return 404. Koha's two answers would give staff two different errors for input that is plainly wrong. |
| Koha's 404 means "not found" only when its body has `error_code` `not_found`. | Treat every 404 as "not found". | Test Koha, 26.05: a missing item returns 404 `{"error":"Item not found","error_code":"not_found"}`, and a path that Koha does not know returns an HTML 404 without it. A wrong base URL must not tell staff that an item does not exist. |
| Whitespace around the number and leading zeros are rejected. | Accept and normalise them, as the first attempt did. | No client needs them. A strict rule can be loosened later without breaking a client; a loose one cannot be tightened. Test Koha, 26.05: `007` returns 400. This differs from the first attempt's contract, which trimmed spaces. |
| A number above 2147483647 is invalid (400). | Pass it to Koha. | Koha's `item_id` is a 32-bit integer in the contract. Test Koha, 26.05: `2147483648` returns 404, so Koha would say "not found" for a number that can never exist. |
| Redirects from Koha are not followed. | Follow them. | A redirect means the base URL is wrong. Test Koha: plain `http` answers 301. Following it would hide the wrong setting. |
| A response handler on both Koha HTTP clients corrects the content type `charset=utf8` to `utf-8`. | Leave it. | Test Koha, 26.05, 2026-11-23: a 401 carries `Content-Type: application/json; charset=utf8`. .NET 10 refuses to read that body, so the generated client would throw an error without a status, and a 401 or 403 would be logged as `system.unexpected` instead of an authentication failure. Found by the second brief check. |
| The text of `not_found` and `method_not_allowed` in part 3. | None. | The maintainer chose the codes; the wording is the agent's, for the maintainer to accept with this brief (`ER-003`). |
| `MINIOASIS_KOHA_BASE_URL` must be `https`, except for a loopback host. | Any `http` address; or `https` only. | RFC 6749, sections 2.3.1 and 3.2, and RFC 6750, section 5.3, require TLS wherever the secret or the token travels. Traffic to the same machine never leaves it, so `http` there exposes nothing, and Koha on the same machine is often plain HTTP. |
| A new error category, "method not allowed", with status 405, is added to the table of `ER-004`. | Leave the framework's empty 405; or answer a wrong method with 404. | `ER-011` allows a client-visible message only for a registered definition, and every definition needs a category. An empty 405 breaks the maintainer's "one shape" decision; a 404 would misreport the cause. This pull request makes that one-row edit to `docs/best-practices/errors.md`. |

**Needs your decision**

None.

**Least sure of**

- **The base URL rule.** If production Minioasis reaches Koha over plain HTTP on another machine of a private network, the application will refuse to start, and this decision must change before transfer.
- **The 405 category.** It is the only edit to a rule file. If the maintainer prefers no rule change, the alternative is to drop the 405 row and leave the framework's answer.
- **Koha's answers outside the contract.** The contract documents only 200, 400, 404, 500, and 503 for `getItem`. The content type of a real 403 is unknown, because the test account has the permission. The whole item is read for four fields, so one field that departs from the contract makes every read a 502.
- **"Method not allowed" as an Application category.** It is an HTTP idea among business categories. It is there only because `ER-011` ties every client-visible message to a registered definition.

## 5. Not included

- **Staff access control.** The next brief: the API validates the Cloudflare Access token that the user interface forwards. Until then the route is open to whoever can reach the API.
- **Lookup by barcode,** and item status, availability, and location. Added to the roadmap.
- **A health route, and Docker files.**
- **An OpenAPI document of Minioasis's own API.** No client needs it yet.
- **Retrying Koha** beyond the one retry after a 401.

## 6. Behaviors that must have a test

`[K]` marks a behavior that also has a check against the test Koha (`TS-013`).

**Get item**

1. An existing item returns 200 with `id`, `bibliographicRecordId`, `barcode`, and `callNumber` as part 3 defines them. `[K]`
2. An item without a barcode or a call number returns 200 with `null` for the missing field.
3. (removed)
4. Each invalid `id` returns 400 `items.invalid_identifier`, and Koha is not called: a space (`%20`), a space before a number (`%2012`), `007`, `abc`, `1.5`, `+5`, `-1`, `0`, `2147483648`. The use case also rejects an empty string, which no HTTP request can send.
5. A number that Koha does not have returns 404 `items.not_found`, and the detail holds the number. `[K]`
6. Each Koha answer in the translation table of part 3 gives the outcome and status in its row. The test names: 401 twice and 403, each with the content type `application/json; charset=utf8`; 429, 500, 502, 503, 504, 599, a connection failure, a cut-off response, the deadline expiring while the item is requested, 204, 301, 400, 409, 418, a 404 with an HTML body, an unreadable body, `item_id` 0, `biblio_id` missing.
7. The item is read correctly when the server's culture is not English.

**Koha connection**

8. The token request sends `grant_type=client_credentials` in the form body and the identifier and secret in the Basic header. The secret is never in the body. `[K]`
9. A token response is rejected unless the token is not blank, `expires_in` is positive, and `token_type` is `Bearer` in any letter case.
10. A token is reused until 60 seconds before it expires, and a new one is requested after that.
11. Callers that need a token at the same moment share one token request. When one of them cancels, the others still get the token. The shared request is itself bounded by the deadline of behavior 16.
12. Each failure to get a token is an authentication failure: status 403 with `access_denied` `[K]`, status 500, a connection failure, a timeout, an unreadable body, a response that behavior 9 rejects, the deadline expiring while the token is requested.
13. The token request does not pass through the handler that attaches tokens, and it is never retried.
14. After a 401 on a GET, the cached token is discarded, a new one is requested, and the request is sent once more. A second 401 is returned. `[K]`
15. A request other than GET is never sent again after a 401.
16. Token request, item request, retry, and reading the body together take at most 10 seconds. When the deadline expires, the result is a dependency failure, not a cancellation.
17. When the caller cancels, the work stops, no failure is returned or logged, and no error response is written.
18. The application refuses to start, naming the setting and no value, for each of: a missing or blank setting (each of the three), a base URL that is relative, has a scheme other than `http` and `https`, is `http` with another host than the three of part 3 (`http://koha.internal`, `http://127.0.0.2`), holds a user name or password, or has a path, query, or fragment. It starts with `https://koha.example.org`, `https://koha.example.org/`, and `http://localhost:8081`.

**Error contract**

19. Every error response has the shape and content type of part 3, with `code` and `traceId`, and without `type` and `instance`.
20. Each category gives its status: validation 400, not found 404, method not allowed 405, unusable dependency response 502, dependency unavailable 503, dependency authentication 500, unexpected 500.
21. A 500, 502, or 503 has the title "Internal error", the code `internal_error`, and no `detail`, the same in Development and in Production, for a returned failure and for an unhandled exception.
22. For each server failure, the log holds the internal error code and the operation name, or the exception, with the `traceId` that the response carries.
23. An unknown path and `GET /api/v1/items/` return 404 `not_found`. `POST /api/v1/items/1` returns 405 `method_not_allowed` with the header `Allow: GET`. All are in the shape of behavior 19.
24. The application refuses to start on each inconsistency that `ER-011` lists between the error registry and `errors.en.json`.
25. No log line and no response holds the client secret, an access token, or an `Authorization` header, on the success path and on each failure path of behaviors 12 and 6.

**Added after the brief check**

26. When every token is invalid, so that Koha answers 401 twice, the adapter reports an authentication failure. `[K]`
27. The requests go to `<base URL>/api/v1/oauth/token` and `<base URL>/api/v1/items/{item_id}`, with one `/` between the parts, with and without a trailing `/` in the setting.

## 7. How it will be built

Three layers, as `docs/best-practices/architecture.md` describes. Application holds the use case, the identifier, the gateway interface, the result and outcome types, and the error definitions with their registry. Api holds the endpoint, the one mapper from result to HTTP, the Problem Details factory, the exception handler, the message file, and the startup check. Koha holds the options, the token provider, the bearer and deadline handlers on two named HTTP clients (`EX-007`), the generated clients, and the adapter that translates Koha's answers.

| Project | Package | Version | Why |
| --- | --- | --- | --- |
| `Minioasis.Application` | `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.15 | Its registration method (`AR-005`). |
| `Minioasis.Koha` | `Microsoft.AspNetCore.App` (framework reference, not a package) | .NET 10 | `IHttpClientFactory`, options, configuration, and logging, without separate packages. |
| `Minioasis.Api.Tests` | `Microsoft.AspNetCore.Mvc.Testing` | 10.0.15 | Runs the API in memory for the endpoint and error tests. |

Each production project lets its own test project see its internal types. The code reads the time from .NET's built-in `TimeProvider`, and the tests replace it with a handwritten fake (`TS-007`); no package is needed. The definitions `not_found`, `method_not_allowed`, `system.unexpected`, and the three `koha.*` definitions belong to no single feature, so they sit in shared groups in Application.

- **Contracts:** `contracts/oauth.openapi.json` and `contracts/items.openapi.json`, each copied faithfully from the official contract (`EX-002`). The clients are generated by NSwag into `Minioasis.Koha/Generated/OAuth` and `Minioasis.Koha/Generated/Items` (`EX-003`). The build writes the exact generation command into `docs/running.md`.
- **Test Koha checks:** in `Minioasis.Koha.Tests`, marked so that they can be run alone, and skipped when the five `MINIOASIS_TEST_KOHA_*` variables are absent. They add the two Cloudflare Access headers through a test-only handler; the application's code does not know about them. `MINIOASIS_TEST_KOHA_BASE_URL` has the format of `MINIOASIS_KOHA_BASE_URL`; the maintainer adds `https://` to its present value, which is a bare host. The check for behaviors 1 and 5 creates a bibliographic record and an item (`POST /biblios`, `POST /biblios/{biblio_id}/items`), reads the item through the adapter, and deletes both. These set-up and clean-up calls are handwritten HTTP calls in the test project only. Their paths and fields come from the official contract (`EX-001`); they are not in the focused contracts, because the application never makes them. `POST /biblios` takes MARC-in-JSON with the content type `application/marc-in-json`. The check for behavior 14 gives the token provider an invalid token first: the test Koha answers 401 to it, and the retry succeeds. The test Koha's service account may do this (26.05, 2026-11-23: 200 and 201); the application's own account needs only `catalogue`. The build writes into `docs/running.md` how the checks are run.
- **Rule file:** `docs/best-practices/errors.md`, `ER-004`, gains the row "Method not allowed: the route does not support the request method | 405" in this pull request.

## Acceptance

<Added at transfer.>
