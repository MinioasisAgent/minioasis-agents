- **Access:** Level 1
- **Last Updated:** 2026-11-25

# 002 — Staff access control

- **Status:** draft
- **Changes data in Koha:** no
- **Related:** `docs/briefs/001-get-item.md` (the error contract), `FE-004` (the production path)

## 1. Goal

The API answers only a request that carries a token which Cloudflare Access issued for this application to the one staff member's email. Every other request is refused before any work is done, so that the API is no longer open to whoever can reach it.

**The two boundaries.** Cloudflare Access is the first boundary. It protects the whole staff address: the frontend's HTML, CSS, JavaScript, and other static files, and every API route. No path is left out, so a person who is not signed in cannot even load the React application. The backend's own check of the token and of the email is the second boundary. It is independent of the first, and it stops a request that got past Cloudflare Access or never went through it. This brief builds the second boundary and states the first (`FE-004`, and `docs/transfer.md`, section 2).

## 2. What people do and see

This capability has no screen. The steps describe a client of the API.

1. The staff member is signed in through Cloudflare Access with the Google account. The frontend calls `GET /api/v1/items/12` with the header `X-Requested-With: XMLHttpRequest`, as it does on every call (`FE-004`). Cloudflare adds the header `Cf-Access-Jwt-Assertion`, and nginx passes it on unchanged.
2. The backend validates that token, and answers as brief 001 says.
3. If the token is missing, expired, or not valid for this application, the client gets 401 "Sign-in required". Nothing else is done, and Koha is not called. What the frontend does on a 401 is written in `FE-004`, not here.
4. If the token is valid but its email is not the configured one, the client gets 403 "Access denied". The frontend shows it like any other error.
5. If the token and the email are right but `X-Requested-With` is missing or has another value, the client gets 403 "Request refused". The Minioasis frontend never causes this. It is a request that another website made the browser send.
6. If the backend needs Cloudflare's signing keys to decide and cannot get them, the client gets "Internal error" with a `traceId`, and the log holds the cause.
7. Steps 3 to 6 apply to every path, including one that matches no route. A caller without a valid token never learns which routes exist.

## 3. The API

No route is added. Every route, present and future, gets the four answers below.

| Situation | HTTP status | Public code | Title / detail shown to staff |
| --- | --- | --- | --- |
| The token is missing or fails a check below | 401 | `unauthenticated` | "Sign-in required" / "Your sign-in could not be confirmed." With the header `WWW-Authenticate: CloudflareAccess`, exactly that value. |
| The token is valid, and its email is not the configured one | 403 | `access_denied` | "Access denied" / "This account may not use Minioasis." |
| The token and the email are right, and `X-Requested-With` is not exactly `XMLHttpRequest` | 403 | `request_refused` | "Request refused" / "This request did not come from Minioasis." |
| The backend must fetch the signing keys to decide (see "The keys"), and the fetch fails | 503 | `internal_error` | "Internal error", no detail (`ER-006`) |

All four are Problem Details in the shape of brief 001. The 401 never says which check failed.

**The checks on the token.** It is read only from the header `Cf-Access-Jwt-Assertion`. It is valid when all of these hold:

- it is a signed JWT with the algorithm `RS256`, and its signature matches a key that Cloudflare publishes at `<team domain>/cdn-cgi/access/certs`;
- its `iss` equals the team domain, as the scheme and the host without a trailing `/`, whether or not the setting ends in one;
- its `aud`, a string or a list, holds the configured audience;
- its `type` is `app`;
- it has an `exp`, and the time is before it. An `nbf`, when present, has passed. The allowed clock difference is 60 seconds.

Then the email: the token's `email` must equal the configured email, compared without regard to letter case. A token without an `email`, such as a service token's, is refused with the 403.

**The keys.** The backend holds the keys it last fetched, in memory. It fetches them from `<team domain>/cdn-cgi/access/certs` in two cases only: it holds none, or a token names a key that it does not hold and the last fetch started more than 60 seconds ago. The request that caused the fetch waits for it and is decided with the fetched keys, which replace the held ones. Requests that need a fetch at the same moment share one. A fetch takes at most 10 seconds. When a fetch fails, the request that waited for it gets the 503, and the held keys stay. A token that names a key the backend does not hold, less than 60 seconds after the last fetch started, gets the 401 without a fetch.

**Order.** The token is checked first, then the email, then `X-Requested-With`, then routing. So an unknown path without a token is 401, not 404.

**Internal code, logged only:** `access.keys_unavailable`.

**Configuration**

| Name | Value |
| --- | --- |
| `MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN` | The team's address at Cloudflare Access: an absolute `https` URL without a path, query, or fragment, for example `https://example.cloudflareaccess.com`. A trailing `/` is accepted. A user name or password in the URL is refused. `http` is never accepted. |
| `MINIOASIS_CLOUDFLARE_ACCESS_AUDIENCE` | The application's audience tag, which Cloudflare shows for the Access application. |
| `MINIOASIS_CLOUDFLARE_ACCESS_EMAIL` | The one email address that may use Minioasis. It is set where the application runs and is never written in the repository. |

If any of the three is missing, blank, or malformed, the application refuses to start, names the setting, and shows no value. An email is malformed when it does not have exactly one `@` with text on both sides, or holds a space.

**Koha operations:** none.

**Security**

- **Who may call it:** only a caller with a token that Cloudflare Access issued for this application to the configured email.
- No patron data is involved.
- **Logging.** A refused request logs the reason (missing, malformed, wrong algorithm, unknown key, bad signature, wrong issuer, wrong audience, wrong type, expired, not yet valid, wrong email, header missing) and the `traceId`. No log line and no response ever holds the token, its `email`, `sub`, `iss`, `aud`, or key name, or the configured email. An accepted request logs nothing about the person.

## 4. Decisions

**Made by the maintainer**

| Decision | Reason | Date |
| --- | --- | --- |
| One staff member. No password sign-in in Minioasis. Cloudflare Access signs the person in with Google, and the backend validates Cloudflare's token. | The maintainer's deployment. | 2026-11-23 |
| Cloudflare Access protects the whole staff address, the frontend's files included. It is the first boundary. The backend's own validation of the token and the email is the second, as defence in depth against a request that bypasses the first. | Nobody who is not signed in may load even the application. Neither boundary relies on the other. | 2026-11-25 |
| Nginx passes the token on unchanged, and frontend code never touches it. | `FE-004`. | 2026-11-24 |
| Nothing in the application's code exists only for a test setup. | "It shall not live in my repo." | 2026-11-25 |
| The backend also checks that the token's email is exactly one configured email. The email is a setting, never in the repository. | Part of the second boundary: it holds even if the Access policy is wrong. | 2026-11-25 |
| Everything runs in Docker, except Koha. The Compose file publishes the backend's port on `127.0.0.1` only. The backend itself does not check its listening address. | Inside a container the backend cannot listen on localhost and still be reached. | 2026-11-25 |
| On a 401 the frontend reloads the page by itself. | The person should not have to. | 2026-11-25 |
| Agents may reach Cloudflare Access from cloud sessions. | So that a check can use Cloudflare's real keys. | 2026-11-25 |

**Made by the agent**

| Decision | Alternative rejected | Reason and evidence |
| --- | --- | --- |
| The token is read from the header `Cf-Access-Jwt-Assertion` only. | Also the cookie `CF_Authorization`. | Cloudflare's documentation, "Validate JWTs", read 2026-11-25: "We recommend validating the `Cf-Access-Jwt-Assertion` header instead of the `CF_Authorization` cookie, since the cookie is not guaranteed to be passed." |
| Only the application token is validated. | Also the global session token. | Cloudflare's documentation, "Session management", read 2026-11-25: the global session token is stored on the team domain and "cannot be used to directly access an application". It never reaches the backend. The check `type` is `app` refuses one that somehow does. |
| Every request must carry `X-Requested-With: XMLHttpRequest`, on every method, and the backend refuses one without it. This is the defence against a request that another website makes the browser send. | No check in the backend, relying on the `SameSite` setting of Cloudflare's cookie. | The frontend must send this header anyway: Cloudflare's documentation, "Session management", section "AJAX", read 2026-11-25, says that Access answers an expired sign-in with 401 only when the request carries it. So the check costs the frontend nothing. A page of another website cannot add the header: the browser first asks the backend's permission, and the backend gives none, because there is one address and no CORS (`FE-004`). Unlike the cookie setting, this check is in the repository and has a test. The cookie of the test Koha's application carries `SameSite=none` (seen 2026-11-25), so the cookie alone stops nothing there. |
| The reload on a 401 happens one time. | Reload on every 401. | A wrong audience setting gives 401 while Cloudflare still sees a signed-in person. An unguarded reload would then loop for ever. |
| A refused token is 401, and a wrong email or a missing header is 403, both in Problem Details. The 401 carries `WWW-Authenticate: CloudflareAccess`. | 401 for all three. `WWW-Authenticate: Bearer`. | RFC 9110, sections 15.5.2 and 15.5.4: the person with a wrong email is known, and may not. A reload cannot help them, so the frontend must be able to tell the two apart. The RFC requires a challenge on a 401 that applies to the resource. `Bearer` (RFC 6750) tells a client to send the token in `Authorization`, which behavior 5 refuses. RFC 9110, section 11.1, allows a scheme name of our own. The header carries no parameters, so it never says which check failed. |
| The email is compared without regard to letter case. | An exact comparison of every character. | A Google address means the same in any letter case. A setting typed with a capital letter must not lock the person out. |
| The checks run before routing, on every path. | Only on matched routes, as the framework does by default. | An outsider learns nothing about the routes. Brief 001's `not_found` and `method_not_allowed` now need a valid token first. |
| The keys are fetched as "The keys" in part 3 says: on the first request, and again when a token names a key that is not held, at most once every 60 seconds. The request waits for the fetch. | Fetch once, for a fixed period. Or the library's own refreshing. | Cloudflare's documentation, "Validate JWTs": the key changes every 6 weeks by default, and can be changed by hand at any time. The published list holds the current and the previous key, so the next key cannot be fetched ahead of time. The team's real key address returned two `RS256` keys on 2026-11-25. The library refreshes in the background and at most once in 5 minutes, so the person's first request after a key change would get 401, and so could every request for 5 minutes. The 60 seconds bound what a broken client, or a caller that got past Cloudflare Access, can make the backend send to Cloudflare. The second boundary exists for exactly that caller. |
| When a fetch that a request waited for fails, the answer is a masked 503, not 401, also when older keys are held. | 401. | The token may be good, and the backend cannot tell. A 401 would make the frontend reload, which cannot help. |
| Only `RS256` is accepted. | Whatever the token's header names. | A token must not choose its own algorithm (RFC 8725, section 3.1). Cloudflare's documentation and the real token of 2026-11-25 both show `RS256`. |
| No bypass and no "development mode" in the application, and the team domain accepts only `https`. The tests answer the key fetch with a test double. The joint check uses a local stand-in for Cloudflare Access, kept outside the repository: it publishes its own key at `/cdn-cgi/access/certs` over `https` with a throwaway certificate, and signs tokens with that key. The backend is pointed at it with the three settings. | A setting that turns the check off. Or accepting `http` on localhost for the team domain, as the Koha address does. | One code path, which is the one production runs. A switch that turns the check off can be left on in production. `http` on localhost would exist only for the stand-in, against the maintainer's decision above; production's team domain is always `https`. |
| This brief does not create `contracts/minioasis.openapi.json`. The frontend foundation creates it, with the 401 and the 403 of this brief on every route. | Create it in this brief's pull request, as `docs/process.md`, section 3, says. | The file does not exist, and nothing reads it until the frontend exists. `docs/ROADMAP.md` already gives it to the frontend foundation. This is an intended exception to that section, for the maintainer to accept with this brief. |
| The text of the three new messages, and the names of the three codes and the three settings. | None. | For the maintainer to accept with this brief (`ER-003`). All are one-way. |

**Needs your decision**

None.

**Least sure of**

- **The joint check's stand-in over `https`.** The team domain accepts only `https`, so the stand-in needs a throwaway certificate that the backend's process is told to trust through the operating system's own settings. Nobody has run this yet. If it cannot work, the first joint check reports it, and the maintainer decides.
- **A person's real token.** The real token I inspected was a service token's, which has no `email`. That a person's token carries `email` as Cloudflare documents it is "needs maintainer" until the first sign-in on the real setup.
- **What Cloudflare answers to an API call after the sign-in ends.** Its documentation says 401 when the request carries `X-Requested-With: XMLHttpRequest`. Nobody has seen it on a real setup: the test Koha's application answers 403 to every request without credentials (2026-11-25). It stays "needs maintainer" until an application with a sign-in page exists.

## 5. Not included

- **Roles, permissions, and more than one staff member.** One person; nothing to tell apart.
- **Recording who did what.** A later capability that writes to Koha may need it.
- **Checking that Google proved the email.** The token does not say which sign-in method was used. The Access application allows Google as its only sign-in method.
- **The Docker and Compose files, the nginx configuration, and the Cloudflare setup.**
- **How the backend's container reaches Koha on the machine.** Inside a container, `localhost` is the container, and brief 001 accepts plain `http` only for `localhost`. It must be settled with the Docker files, before get item is transferred.
- **Calling the API by hand on a development machine.** After this brief every call needs a token. The stand-in of the joint check is not in the repository, so calling by hand works only through the real Cloudflare setup. If the maintainer wants more, that is a later decision.
- **A health route.** When it comes, its brief decides whether it needs a token.
- **Any change to the frontend.** `web/` does not exist yet. Step 3 is written into `FE-004`.

## 6. Behaviors that must have a test

`[C]` marks a check against Cloudflare itself.

1. A request with a valid token for the configured email gets the answer of brief 001: `GET /api/v1/items/12` returns 200.
2. A request without `Cf-Access-Jwt-Assertion`, or with an empty one, returns 401 `unauthenticated` in the shape of part 3, with the header `WWW-Authenticate` equal to `CloudflareAccess` and nothing more. Koha is not called.
3. Each of these tokens returns the same 401: not a JWT; signed with a key that is not published; a changed payload under the old signature; algorithm `none`; algorithm `HS256` signed with the public key as the secret; `iss` of another team; `aud` of another application, as a string and as a list; `type` `org`; no `type`; no `exp`; an `exp` more than 60 seconds ago; an `nbf` more than 60 seconds ahead.
4. A token is accepted with `aud` as a string and as a list that holds the audience among others, with an `exp` that passed less than 60 seconds ago, and when the team domain setting ends in `/` while the token's `iss` does not.
5. A valid token in the cookie `CF_Authorization` or in the `Authorization` header, without `Cf-Access-Jwt-Assertion`, returns 401.
6. An otherwise valid token returns 403 `access_denied` when its `email` is another address, is the configured address with one more character, is empty, or is missing. Koha is not called.
7. A token whose `email` differs from the configured one only in letter case is accepted.
8. A token that fails a check of behavior 3 returns 401, not 403, whatever its `email`.
9. An unknown path, and `POST /api/v1/items/1`, return 401 without a valid token, and brief 001's 404 and 405 with one.
10. The keys are fetched from `<team domain>/cdn-cgi/access/certs`, with one `/` between the parts, on the first request and not at startup. A second request causes no second fetch. Requests that arrive together while no keys are held share one fetch.
11. After the published keys change, the first request whose token is signed with the new key is accepted, after one new fetch, when the last fetch started more than 60 seconds ago. When it started less than 60 seconds ago, that request returns 401 and no fetch is made. A key that is no longer published is refused after the new fetch.
12. When a fetch fails, the request that waited for it gets a masked 503 with `internal_error`, and the log holds `access.keys_unavailable` with the response's `traceId`. This covers a connection failure, a status 500, a body that is not a key set, and a fetch that takes longer than 10 seconds, each with no keys held and with keys held.
13. After a failed fetch, tokens signed with the keys already held are still accepted.
14. The application refuses to start, naming the setting and no value, when any of the three settings is missing or blank; when the team domain is relative, is `http` (`http://example.cloudflareaccess.com`, `http://localhost:8082`), or has a path, a query, a fragment, or a user name; and when the email is `staff`, `@example.org`, `staff@`, or `a b@example.org`. It starts with `https://example.cloudflareaccess.com`, with and without a trailing `/`.
15. No log line and no response holds the token, the value of its `email`, `sub`, `iss`, or `aud`, its key name, or the configured email, on the accepted path and on each refused path of behaviors 2, 3, 6, and 17.
16. A token that Cloudflare Access issued passes every check of behavior 3 against the keys fetched from Cloudflare: the answer is 403, because a service token has no email. The same token with one changed character returns 401. `[C]` The check gets its token from the test Koha's address with the service token of `TS-013`, and reads the team domain and the audience from that token. It belongs to the test Koha checks: it carries the same mark, is skipped when the variables are absent, and falls under the same rules for a merge (`docs/process.md`, sections 12 and 13).
17. With a valid token for the configured email, a request without `X-Requested-With`, or with another value, returns 403 `request_refused`, for GET and for POST. Koha is not called.
18. Without a valid token the answer is 401, and with a wrong email it is 403 `access_denied`, whether or not `X-Requested-With` is there.

## 7. How it will be built

All of it is in `Minioasis.Api`, because staff sign-in is a matter of HTTP (`AR-001`, `AR-003`). ASP.NET's own JWT bearer authentication does the validation: it reads the token from the Cloudflare header, validates it with the parameters of part 3, and takes its keys from a small key source of our own. The key source is our code because Cloudflare's key address is a key set and not an OpenID Connect discovery document, and because the library's own refreshing does not do what part 3 says: it refreshes in the background, so the request with a new key is refused, and it answers a failed fetch as an unknown key (read in the source of `Microsoft.IdentityModel` 8.19.2, which the package brings). The library's own log lines for a refused token hold the token's `iss` or key name, so they must not reach the log (behavior 15). The challenge header is set by us, without the library's error details. One piece of middleware in front of routing asks for the authentication result, then compares the email, then checks `X-Requested-With`. The three new errors are errors of HTTP itself and sit beside `not_found` in Api (`ER-004`), with their text in `errors.en.json`. `access.keys_unavailable` is a shared definition in Application with the category "a dependency is unavailable", beside the `koha.*` codes. The three settings follow the pattern of the Koha options and their validator.

| Component and project | Package | Version | Why |
| --- | --- | --- | --- |
| Backend, `Minioasis.Api` | `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.15 | Signature, issuer, audience, and lifetime checks. Writing these by hand is how validation goes wrong. It is Microsoft's package, on the version of the other ASP.NET packages here. It brings `Microsoft.IdentityModel.*` with it. |

No other package. The tests create an RSA key in memory, answer the key fetch with an `HttpMessageHandler` double (`TS-004`), and sign their own tokens with the library that the package brings. The existing endpoint tests of brief 001 send a valid token and the header.

**Rulebook changes in this brief's pull request.** The first four are safeguard paths that need the maintainer's approving review.

- `FE-004`: it states the two boundaries, and that Cloudflare Access protects the whole address. The two sentences that wait for this brief become statements. The backend answers 401 in Problem Details, requires `X-Requested-With` on every call, and on a 401 the frontend reloads the page one time.
- `docs/process.md`, section 8: a second helper for the joint check stands in for Cloudflare Access, and adds a token it signed.
- `TS-013`: one sentence that lets a test Koha check read the public signing keys of the Cloudflare Access team in front of the test Koha.
- `docs/transfer.md`, section 2: the Access application's settings. It covers the whole address with no path left out, its cookie is `HttpOnly`, it has no CORS settings, Google is the only sign-in method, and the policy admits the one email.
- `docs/ROADMAP.md`: the frontend foundation's contract file includes the 401, the two 403 answers, and the masked 503.

The build also updates `docs/running.md` (the three settings, and how to call the API by hand) and `docs/STATE.md`.

## Acceptance

<Added at transfer.>
