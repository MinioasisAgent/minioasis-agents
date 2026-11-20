- **Access:** Level 1
- **Last Updated:** 2026-11-20

# 001 — API errors and Koha connection

- **Status:** draft
- **Kind:** internal
- **Related:** ADR 0003; `ER-001` to `ER-011` in `docs/best-practices/errors.md`; `EX-004` to `EX-013` in `docs/best-practices/external-systems.md`. This spec combines ROADMAP items 001 (API error contract) and 002 (Koha connection) at the maintainer's request.

## Goal

Every error the API returns has one shape that a client can rely on, server failures reveal nothing but a trace identifier that leads to the logged cause, and every later capability can call Koha through one shared, authenticated, time-limited connection without building any of it again.

## Background

From the repository on 2026-11-20 (`docs/STATE.md`):

- The foundation exists: `minioasis.slnx`, `Minioasis.Application`, `Minioasis.Api`, `Minioasis.Koha`, and a test project for each. `Minioasis.Api` starts an empty host with no endpoints. There is no behavior and no test.
- The rules for the error contract (`ER-001` to `ER-011`) and for Koha authentication (`EX-004` to `EX-013`) are written, but nothing implements them. `Minioasis.Api/Resources/errors.en.json` and `Minioasis.Koha/Generated` do not exist yet.
- `contracts/official_koha_openapi.json` has the token operation `tokenOAuth`: `POST /oauth/token` under the server URL `/api/v1`, with a form body (`grant_type`, `client_id`, `client_secret`) and a JSON reply (`access_token`, `token_type`, `expires_in`). No focused contract exists yet.
- Without this spec, ASP.NET answers an unknown route or a wrong method with an empty body, and an unreadable body in its own shape.

## Staff and roles

No staff member uses this directly. It is what staff see whenever something goes wrong in any later capability, and what the person who runs Minioasis reads in the log and sets in the environment.

## Staff workflow

1. A staff member's screen sends a request that fails. The screen receives an error with a `code` it understands, and, for a problem the staff member can act on, a title and detail it can show.
2. When the failure is on the server or in Koha, the staff member sees only that something failed, with a trace identifier to quote to support.
3. Support searches the log for that trace identifier and finds the real cause.
4. The person who runs Minioasis sets four environment variables. If one is wrong, Minioasis refuses to start and says which variable is wrong.

## Decisions

**Locked**

- **D1.** ROADMAP items 001 and 002 are one spec. — 2026-11-20
- **D2.** Errors that ASP.NET produces before Minioasis code runs also use the Problem Details shape, with the public codes `not_found` (404), `method_not_allowed` (405), and `request.invalid` (400), and the titles and details listed under "Errors". A wrong method is answered with 405 and an `Allow` header; `errors.md` `ER-004` gains a row for it. An unreadable request body, including a wrong content type, is answered with 400 `request.invalid`, not 415. — 2026-11-20
- **D3.** A client-visible error carries one `code`, one `title`, and one `detail`. No list of field errors. — 2026-11-20
- **D4.** `traceId` is the W3C trace-context identifier of the request (`00-<32 hex>-<16 hex>-<2 hex>`), the same value in the response and in the log. — 2026-11-20
- **D5.** Minioasis checks its configuration when it starts and refuses to start when a required variable is missing or blank, when the base URL is not absolute, or when its scheme is not `https`. Plain `http` is allowed only in the `Development` environment. — 2026-11-20
- **D6.** Minioasis does not contact Koha when it starts. Until Koha can be reached, requests that need Koha fail with the masked response; when Koha cannot be reached at all (network error, deadline, 429 or 5xx), at the token step or after it, the outcome is "unavailable", 503 (R20). This makes the network-and-deadline part of `EX-013` an exception, recorded in `EX-013` in the same pull request. — 2026-11-20
- **D7.** The deadline for one Koha interaction is read from a new optional variable, `MINIOASIS_KOHA_TIMEOUT_SECONDS`: whole seconds from 1 to 120, default 10 when absent; any other value stops startup. ADR 0003 and `EX-005` list it with the other variables. — 2026-11-20
- **D8.** This spec defines the shared translation of Koha failures (the table under R20). A capability adds only the outcomes specific to it, for example 404 for a record that does not exist. — 2026-11-20
- **D9.** Logs carry the internal error code, Koha's HTTP status when there is one, and the `traceId`. They never carry an access token, the client secret, an `Authorization` header, or a body that Koha returned. — 2026-11-20
- **D10.** The maintainer runs manual acceptance on their own machine. For this spec it covers the startup check and the error shape (scenarios M1 to M3). The Koha token path is checked in the manual acceptance of the first capability that calls Koha, because nothing in this spec makes a request reach Koha. — 2026-11-20

**Agent's discretion** (the choice is recorded in `plan.md`)

- How long before expiry a cached token is refreshed (the refresh margin), between 10 and 60 seconds.
- Internal names, the internal error codes' exact spelling, and the structure of the handlers, providers, and mapper, within `docs/best-practices/`.
- Where the three framework error definitions live, provided they are in the registry and covered by the startup check (R9).

**Deferred** (added to `docs/ROADMAP.md`)

- A list of field errors in validation responses (D3), when a capability needs it.
- A health endpoint that checks Koha (D6). Already in "Ideas not yet scheduled".

## Requirements

Rules referred to are in `docs/best-practices/errors.md` (`ER-`) and `docs/best-practices/external-systems.md` (`EX-`).

### Error responses

In R5 and R5a, "log entry" counts only entries written by Minioasis code (categories starting with `Minioasis`), except as R5 and R5a state for all categories.

- **R1.** Given an Application failure of each category, when Api turns it into a response, then the status is: validation 400, not found 404, method not allowed 405, dependency returned an unusable response 502, dependency unavailable 503, authentication to a dependency failed 500, unexpected failure 500. The mapping is applied in one place (`ER-004`). Covers each of the seven categories.
- **R2.** Given a client-visible error (statuses 400, 404, 405), when it is returned, then the response has content type `application/problem+json` and a JSON body with exactly the members `status` (number, equal to the HTTP status), `title`, `detail`, `code`, and `traceId`, all strings except `status`. `title` and `detail` are the entry for `code` in `Minioasis.Api/Resources/errors.en.json`, with each `{name}` placeholder replaced by the value the failure supplied. A failure that lacks a value for a placeholder of its message, or supplies a value for a name that is not a placeholder, is a programming error: it is answered as an unhandled exception (R3, R5). The body has no `type` and no `instance` (`ER-003`, `ER-005`). Covers: a message without placeholders, a message with one placeholder, a message with two placeholders, a missing value, an extra value.
- **R3.** Given a server failure, when it is returned, then the status is 500, 502, or 503 as R1 decides and the body is exactly `{ "status": <status>, "title": "Internal error", "code": "internal_error", "traceId": "<traceId>" }`, with content type `application/problem+json` and no `detail` (`ER-006`). Covers: an Application failure of each of the three server categories and of "unexpected failure", and an unhandled exception, each in the `Development` and the `Production` environment.
- **R4.** Given any request that ends in an error response, when the response is written, then `traceId` matches `^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$`, and every log entry that R5 or R5a requires for that request carries the identical value. Two separate requests get different values. Covers: a client-visible error, an Application server failure, an unhandled exception.
- **R5.** Given a server failure, when it is returned, then exactly one log entry at level `Error` is written for it: for an Application failure it contains the internal error code and the `traceId`; for an unhandled exception it contains the exception and the `traceId` (`ER-007`). The Koha connection writes no `Error` entry of its own: the entry required by R20 is this one. ASP.NET's own `Error` entry for an unhandled exception is suppressed, so that across all categories exactly one `Error` entry exists for the failure. Covers: an Application server failure of each server category, an unhandled exception.
- **R5a.** Given a client-visible error, when it is returned, then exactly one log entry at level `Information` is written with its public code and the `traceId`, and no entry at level `Warning` or above in any category, including ASP.NET's. Covers: `not_found`, `method_not_allowed`, `request.invalid`, and a capability's client-visible error.
- **R6.** Given a request to a path that matches no route, when it is answered, then the status is 404 and the body is as R2 with code `not_found`. Covers: an unknown path, an unknown path under a known prefix, the root path `/`.
- **R7.** Given a request to a path that matches a route but with a method the route does not accept, when it is answered, then the status is 405, the body is as R2 with code `method_not_allowed`, and the `Allow` header lists exactly the methods the route accepts. Covers: a route with one method, a route with two methods.
- **R8.** Given a request to an endpoint that reads a JSON body, when the body cannot be read, then the status is 400 and the body is as R2 with code `request.invalid`. A `Content-Type` is accepted when its media type, compared without regard to case and ignoring parameters such as `charset`, is `application/json` or `application/<anything>+json`. Covers: malformed JSON, a JSON value of the wrong type for a field, an empty body where one is required, a missing `Content-Type`, `text/plain` (answered 400, not 415), a body over the server's size limit (answered 400, not 413). Accepted: `application/json; charset=utf-8`, `Application/JSON`, `application/problem+json`.
- **R8a.** Given a request to a matched route, when a route or query value cannot be converted to the endpoint's parameter type, or a required query value is missing, then the status is 400 and the body is as R2 with code `request.invalid`. A value that fails a route constraint means no route matches (R6). Covers: a non-numeric value for a numeric parameter, a missing required query value.
- **R9.** Given the application starting, when the error definitions and `errors.en.json` are inconsistent, then startup fails before any request is served, naming the offending code where there is one and otherwise the problem, for each class in `ER-011`: a blank code; a duplicate code; a null definition; an undefined category value; an empty registry; a client-visible definition with no message; a message for a server-failure definition; a message for an unknown code; a duplicate message key; a resource file that is not valid JSON; a null message; a message entry that is not an object with `title` and `detail`; a message with a missing or blank `title` or `detail`. Given they are consistent, startup proceeds.

### Configuration

- **R10.** Given the application starting, when `MINIOASIS_KOHA_BASE_URL`, `MINIOASIS_KOHA_CLIENT_ID`, or `MINIOASIS_KOHA_CLIENT_SECRET` is absent, empty, or only whitespace, then startup fails with a message naming that variable. Covers each variable in each of the three states.
- **R11.** Given the application starting, when `MINIOASIS_KOHA_BASE_URL` is not an absolute URL, or its scheme is neither `https` nor `http`, then startup fails with a message naming the variable. When the scheme is `http` and the environment is not `Development`, startup fails with a message naming the variable and saying that `https` is required. A base URL with a query string, a fragment, or user information (`user@`) fails with a message naming the variable. When the scheme is `https`, or `http` in `Development`, startup proceeds. A trailing slash is optional: `https://koha.test/api/v1` and `https://koha.test/api/v1/` give the same base. Covers: a relative path, plain text, `ftp://…`, `http://…` in `Development` and in `Production`, `https://…` with and without a trailing slash, `https://koha.test/api/v1?x=1`, `https://koha.test/api/v1#x`, `https://u:p@koha.test/api/v1`.
- **R12.** Given the application starting, when `MINIOASIS_KOHA_TIMEOUT_SECONDS` is absent, then the deadline is 10 seconds. When it is a whole number from 1 to 120 written only in the ASCII digits `0` to `9`, then the deadline is that many seconds. Otherwise startup fails with a message naming the variable and the allowed range. Covers: absent, `1`, `10`, `010`, `120`, `0`, `-5`, `+10`, `121`, `10.5`, `ten`, empty, whitespace, ` 10 `, `１０` (full-width digits), `99999999999999999999`.
- **R13.** Given any startup failure from R10 to R12, when its message is written, then it contains the variable's name and never its value.
- **R14.** Given valid configuration, when the application starts, then no request is sent to Koha (D6).

### Koha connection

A "protected call" is any request a capability sends to Koha through the shared Koha client, which every capability uses (`EX-004`). Tests use a fake Koha; no test calls a real Koha.

- **R15.** Given a protected call and no usable cached token, when a token is needed, then Minioasis sends one `POST <base>/oauth/token` with a form body `grant_type=client_credentials`, `client_id`, and `client_secret` from configuration, built from the focused contract derived from `tokenOAuth` (`EX-002`, `EX-006`). The token request carries no `Authorization` header (`EX-007`).
- **R16.** Given a token reply with status 200, when it is checked, then it is accepted only if `access_token` is present and not blank, `expires_in` is a JSON integer from 1 to 2147483647, and `token_type` equals `Bearer` ignoring case. Any other reply is an authentication failure (`EX-008`, `EX-013`). Covers: a valid reply; `token_type` `bearer` and `BEARER`; `access_token` missing, empty, or whitespace; `expires_in` missing, 0, negative, a string (`"3600"`), a fraction (`3600.5`), or 2147483648; `token_type` missing or `mac`; a body that is not JSON.
- **R17.** Given an accepted token, when later protected calls need a token before its expiry minus the refresh margin, then they reuse it without a new token request. At or after that moment, the next call requests a new token. A token whose lifetime is not longer than the refresh margin is used for the call that requested it and is not cached. Covers: a reuse within the lifetime, a refresh after it, a short-lived token. Time is controlled in tests, not waited for.
- **R18.** Given several protected calls that need a new token at the same moment, when they run, then exactly one token request is sent and every call receives its result, success or failure. The shared token request is bounded by the configured deadline from the moment it is sent. Each caller's own deadline (R21) or cancellation (R22) ends only that caller's wait. When the last waiting caller stops waiting, the shared token request is cancelled and its result, if any, is not cached; the next call sends a new token request. A failed token request is not cached: the next call after it sends a new token request. Covers: 10 simultaneous calls with a success; 10 simultaneous calls with a failure, then one more call; one of 10 waiting callers cancelling (the others receive the token); all 10 cancelling (the token request is cancelled, nothing is cached, and the next call sends a new token request).
- **R19.** Given a protected call, when it is sent, then it carries `Authorization: Bearer <token>`. When Koha answers a `GET` with 401, the cached token is discarded if it is still the token that got the 401 (a newer token another call already obtained is kept and used), a new token is requested, and the request is sent once more with the new token; the reply to that second attempt is final (`EX-009`). When Koha answers a request with any other method with 401, the token that got the 401 is discarded in the same way, but the request is never sent again; no request other than `GET` is sent again after any reply (`EX-010`). Covers: `GET` with 401 then 200; `GET` with 401 then 401; `GET` with 401 after another call already refreshed the token; `POST`, `PUT`, `PATCH`, `DELETE` with 401; `GET` with 500 (not retried).
- **R20.** Given a protected call, when its final outcome is one of those below, then the Application outcome, the response status, and the internal code are as follows, unless the capability's spec claims that Koha status for itself (D8). The row is decided by the step that failed: the token step (getting a token, including the new token after a 401) or the call step (sending the protected request, including its retry, and reading its body). A "network error" is any failure to connect, resolve, negotiate TLS, send, or read: in .NET, an `HttpRequestException` or `IOException`. The response is always the masked one (R3), and the single `Error` entry of R5 also carries Koha's status when there is one.

  | Step | Final outcome | Application category | HTTP status | Internal code |
  | --- | --- | --- | --- | --- |
  | token | A network error, the deadline expires, 429, or any status from 500 to 599 | dependency unavailable | 503 | `koha.unavailable` |
  | token | Any other status than 200, or a 200 reply that fails R16 | authentication to a dependency failed | 500 | `koha.authentication_failed` |
  | call | 401 to a `GET` after the retry, 401 to any other method, or 403 | authentication to a dependency failed | 500 | `koha.authentication_failed` |
  | call | A network error, the deadline expires, 429, or any status from 500 to 599 | dependency unavailable | 503 | `koha.unavailable` |
  | call | Any status not in another row and not claimed by the capability, including 100 to 199, 300 to 399, other 4xx, and 600 to 999; a 2xx status that the focused contract does not define for the operation; or a 2xx body that was received in full but cannot be parsed or does not match the focused contract. A connection lost while the body is being read is a network error (the row above) | dependency returned an unusable response | 502 | `koha.invalid_response` |

  Covers each row; for the token step the statuses 400, 401, 403, 429, 500, 599; for the call step the boundary values 399, 400, 499, 500, 599, 600, 999, a 2xx status the contract does not define, a body cut off mid-way (503), and a complete body with a field of the wrong type (502); and a token step that fails during the retry after a 401 (decided by the token-step rows).
- **R21.** Given a protected call, when it runs, then one deadline of the configured length (R12) bounds all of it: getting a token, sending the request, the retry of R19, and reading the whole response body (`EX-011`). Covers: expiry while getting a token (R20, token step: 503), while waiting for response headers, while reading a slow body, and during the retry.
- **R22.** Given a request whose caller cancels it, when the cancellation reaches a protected call, a token request, or the response body, then that caller's outbound protected request is cancelled (a shared token request follows R18), the cancellation propagates unchanged, no failure result is produced, and no `Error` entry is logged (`ER-009`, `EX-012`). Covers: cancellation while waiting for a token, during a token request shared with other callers (only the cancelling caller stops; the others still receive the token), during the protected call, while reading the response body, and during the retry. When the caller's cancellation and the deadline's expiry coincide, the caller's cancellation wins.
- **R23.** Given any Koha interaction, when anything is logged at any level, then no log entry contains the access token, the client secret, the value of an `Authorization` header, or any part of a body that Koha returned. Covers: a successful call, a token request that fails with an error body, a 401 followed by the retry, a call that fails with a Koha error body containing a marker string; tests capture every log entry and check that the token, the secret, and the marker are absent. This applies to entries from every category, including ASP.NET's and `HttpClient`'s.

## Edge cases

| Edge case | State | Requirement, reason, or manual scenario |
| --- | --- | --- |
| A message placeholder has no supplied value, or a value is supplied for no placeholder | covered | R2 |
| The request carries a valid `traceparent` header | dismissed | Whether the incoming trace is continued does not change the contract: R4 still holds for the response and the log |
| An exception is thrown after the response has started | dismissed | The status and headers are already sent and cannot be replaced; the exception is still logged at least once with the `traceId`; R5's count of exactly one does not apply |
| `HEAD` or `OPTIONS` on a `GET` route | covered | R7: answered 405 unless the route accepts the method |
| Kestrel rejects a request before routing (header too large, URI too long) | dismissed | Kestrel answers before any Minioasis code runs, so no middleware can shape the reply |
| `MINIOASIS_KOHA_TIMEOUT_SECONDS` has a sign, leading zeros, spaces, non-ASCII digits, or overflows | covered | R12 |
| Base URL with a query string, fragment, or user information | covered | R11 |
| Koha's token endpoint returns 400 or 403 (wrong client id or secret) | covered | R20, token step: 500 |
| The token expires between the cache check and Koha's receipt of the request | covered | R19: the resulting 401 to a `GET` triggers one refresh and retry |
| Simultaneous token refresh after a 401 retry by several callers | covered | R18 applies to every token request, including those from R19 |
| The real Koha's token reply differs from the contract | manual | D10: checked in the first Koha capability's manual acceptance |
| A 2xx reply with an empty body where the contract has one | covered | R20 last row |
| A Koha response body that arrives slowly | covered | R21 |
| A large Koha response body that arrives quickly | dismissed | Koha is the trusted authority, and a size limit, if one is needed, depends on the operation and belongs to the capability's spec |
| Koha cannot be reached on the first call after startup | covered | R20, token-step row 1: 503 |
| A 413 body too large, or a route or query value that cannot be converted | covered | R8, R8a |

## Prohibitions

| The system must not… | State | Requirement, reason, or manual scenario |
| --- | --- | --- |
| reveal a provider name, internal code, exception, or diagnosis in a 500, 502, or 503 response | covered | R3 |
| return an error body in any shape other than Problem Details | covered | R2, R3, R6 to R8 |
| log an access token, the client secret, an `Authorization` header, or a Koha response body | covered | R23; also manual scenario M3 |
| write a configuration value into a startup error | covered | R13 |
| send a non-`GET` request to Koha more than once | covered | R19 |
| retry a `GET` more than once | covered | R19 |
| request a token for every call while a valid token is cached | covered | R17 |
| send the token request through the client that attaches bearer tokens | covered | R15 |
| treat a caller's cancellation as a failure, or log it as an error | covered | R22 |
| contact Koha during startup | covered | R14 |
| start with an inconsistent error registry or invalid configuration | covered | R9 to R12 |
| call a real Koha from a test | dismissed | A process rule, not behavior: `CLAUDE.md`, Level 3, forbids it, and review enforces it |

## API contract

This spec adds no endpoint. It defines the error responses of every endpoint (R2, R3, R6 to R8). Error body members:

| Member | Type | Present |
| --- | --- | --- |
| `status` | number | always |
| `title` | string | always |
| `detail` | string | client-visible errors only |
| `code` | string | always |
| `traceId` | string | always |

No member is null. Content type: `application/problem+json`.

## Errors

| Situation | HTTP status | Public code | Title / detail shown to staff |
| --- | --- | --- | --- |
| No route matches the path | 404 | `not_found` | "Not found" / "No resource exists at this address." |
| The route does not accept the method | 405 | `method_not_allowed` | "Method not allowed" / "This address does not support this request method." |
| The request body cannot be read | 400 | `request.invalid` | "Invalid request" / "The request could not be read." |
| Any server failure | 500, 502, 503 | `internal_error` | "Internal error" / no detail |

Server failures and their internal codes, logged only (R5, R20):

| Situation | Internal code | Status |
| --- | --- | --- |
| Authentication to Koha failed (R20) | `koha.authentication_failed` | 500 |
| Koha unavailable, at the token step or the call step (R20) | `koha.unavailable` | 503 |
| Koha returned an unusable response | `koha.invalid_response` | 502 |
| Unhandled exception, or an Application failure of category "unexpected" | `system.unexpected` | 500 |

## Koha operations

| Koha operation | Read or write | Required Koha permission | Focused contract |
| --- | --- | --- | --- |
| `tokenOAuth` (`POST /oauth/token`) | read (issues a token; changes no library record) | none beyond a valid API client for the service account | `contracts/oauth.openapi.json` |

## Koha configuration

- The Koha system preference `RESTOAuth2ClientCredentials` must be enabled, and the service account must have an API client (client id and secret). Without them the token request fails, which is the second token-step row of R20. The preference name is from Koha's documentation, not from this repository; the maintainer confirms it in the first Koha capability's manual acceptance (D10).

## Failure and retry

Not a write spec. The only retry is R19: one retry of a `GET` after a 401. Nothing else is retried.

## Security and privacy

No staff authentication exists yet; the API is open to anyone who can reach it (ROADMAP, "Ideas not yet scheduled"). This spec reads no patron data.

| Threat | Mitigation | Requirement |
| --- | --- | --- |
| A server error reveals internals to an attacker | Every server failure is masked | R3 |
| The client secret or a token leaks through logs or startup messages | Never logged or echoed | R13, R23 |
| The secret and tokens travel unencrypted | `https` required outside `Development` | R11 |
| Patron data leaks through logs | Koha response bodies are never logged | R23 |
| A replayed write changes a Koha record twice | No non-`GET` request is ever retried | R19 |
| A slow Koha holds requests open | One bounded deadline | R12, R21 |

## Out of scope

- Any endpoint or capability that calls Koha. The first capability (ROADMAP 003) is the first to send a protected call.
- Staff authentication and authorization.
- A health or readiness endpoint, and any check of Koha at startup (D6).
- Field-level validation errors (D3).
- Retrying, circuit breaking, or rate limiting beyond R19.
- Languages other than English.
- Shaping errors that Kestrel produces before routing.

## Open questions

None.

## Manual acceptance (D10)

The maintainer runs these on their own machine and reports the results, which are recorded word for word in `verification.md`.

- **M1.** Start Minioasis with each of these, and confirm it refuses to start with a message that names the variable and does not show its value: `MINIOASIS_KOHA_CLIENT_SECRET` unset; `MINIOASIS_KOHA_BASE_URL` set to an `http://` address in `Production`; `MINIOASIS_KOHA_TIMEOUT_SECONDS=0`. Then start it with valid values and confirm it starts.
- **M2.** With Minioasis running, send `GET /does-not-exist` and confirm the answer is 404 with the shape and code under "Errors" and a `traceId`. This spec adds no endpoint, so 405 and 400 can only be reached through the automated tests' test endpoints; they are checked again in the first capability's manual acceptance.
- **M3.** Read the log of M1 and M2, confirm the `traceId` values from M2 appear in it, and confirm that the client secret appears nowhere.

## Ready for approval

- [x] The goal is specific enough to check.
- [x] Every requirement is testable, and the cases it covers can be listed.
- [x] Every edge case and prohibition has a state.
- [x] "Out of scope" names the nearest things this spec does not do.
- [x] For a write spec, "Failure and retry" is complete. (Not a write spec.)
- [x] There are no open questions.
