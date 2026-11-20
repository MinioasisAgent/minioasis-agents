- **Access:** Level 1
- **Last Updated:** 2026-11-20

# 001 — API errors and Koha connection: implementation plan

- **Spec:** [spec.md](spec.md)
- **Plan status:** draft

## Approach

The work fills the three projects of `AR-001`; no new project or layer.

- **`Minioasis.Application`** owns the error model: the categories, the definitions, the registry of all definitions, and the result type (`ER-001`, `ER-002`). It also owns `DependencyFailureOutcome`, the three shared Koha failure outcomes of R20, so that the Koha project can report them in Application's own types (`AR-006`).
- **`Minioasis.Api`** owns everything the client sees: the category-to-status mapper (R1), the one factory that builds every error body (R2, R3), the English resource file and its startup check (R9), the exception handler (R3, R5), the shaping of framework errors (R6 to R8a), and the trace identifier (R4). It is the composition root (`AR-005`).
- **`Minioasis.Koha`** owns the connection: configuration and its startup check (R10 to R13), the generated OAuth client, the token provider (R15 to R18), the bearer handler (R19), the deadline handler (R21), and the mapper from HTTP results to `DependencyFailureOutcome` (R20). Capabilities call Koha through the named client `KohaClientNames.Api` and pass the caller's token on (`EX-004`, `EX-012`).

How a failure travels: a capability's adapter (from spec 003 on) catches the documented Koha exceptions and HTTP statuses at its boundary, asks `KohaProtocolFailureMapper` for the shared outcome unless it claims the status itself (D8), and returns a gateway result. The use case selects the definition from `DependencyErrorDefinitions` and puts Koha's status in the error's metadata. Api maps the category to the status, writes the masked body, and writes the single `Error` entry with the internal code, Koha's status, and the `traceId` (R5, R20). The Koha project logs nothing at `Warning` or above.

**Choices made under "Agent's discretion":**

- **Refresh margin:** 30 seconds.
- **Error registry:** built by reflection over the public static `ApplicationErrorDefinition` fields of static classes marked `[ErrorDefinitions]` in the Application assembly, so a new definition cannot be left out of the startup check. A null field is reported by the check (R9).
- **Framework error definitions** live in `Minioasis.Application/Shared/Errors/RequestErrorDefinitions.cs`: `not_found`, `method_not_allowed`, `request.invalid`.
- **Resource file:** `Minioasis.Api/Resources/errors.en.json`, embedded in the assembly, so that publish and tests read the same bytes.
- **Endpoint registration:** Api maps every `IEndpointModule` registered in dependency injection. Today no module exists in production. Tests register a test module to reach R7, R8, and R8a, and the first capability adds the first real one. This is the smallest way to give the test host endpoints without a second host (`TS-009`).
- **The `Development` check:** Api passes `builder.Environment.IsDevelopment()` to `AddKohaIntegration`, so the Koha project needs no hosting package.
- **No interface for the token provider:** only the bearer handler uses it, and tests reach it through `HttpMessageHandler` doubles (`CD-008`, `TS-004`). Its documented exceptions are its contract (`CD-005`).
- **Internal codes:** exactly as the spec's "Errors" table spells them.

## One-way decisions

| Decision | What undoing it would break | Settled by |
| --- | --- | --- |
| Error body members, types, and content type | Every client's error handling | R2, R3, D3 |
| Status per category, including 405 | Clients that branch on status | R1, D2, `ER-004` |
| Public codes `not_found`, `method_not_allowed`, `request.invalid`, `internal_error`, with their titles and details | Clients that branch on code; staff screens | D2, R3, spec "Errors" |
| `traceId` format | Support tooling and log searches | D4, R4 |
| `MINIOASIS_KOHA_TIMEOUT_SECONDS`: name, unit, range, default | Every deployment's configuration | D7, R12 |
| `https` required outside `Development` | Deployments that use `http` | D5, R11 |
| 503 for an unreachable Koha at the token step | Monitoring that reads statuses | D6, R20 |
| No Koha contact at startup | Deployments that expect a start to fail when Koha is down | D6, R14 |

## Dependencies

Each is a new dependency (Level 2, item 4), accepted with this plan. Versions match the installed .NET runtime, 10.0.15. If a version cannot be restored, the coder stops and reports it (`docs/workflow/implementation.md`).

| Project | Package | Version | Why |
| --- | --- | --- | --- |
| `Minioasis.Application` | `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.15 | `AddApplication()` registers the registry (`AR-005`) |
| `Minioasis.Koha` | `Microsoft.Extensions.Http` | 10.0.15 | `IHttpClientFactory`, named clients, handler pipeline (`EX-007`) |
| `Minioasis.Koha` | `Microsoft.Extensions.Configuration.Abstractions` | 10.0.15 | Reads the four variables (`EX-005`) |
| `Minioasis.Koha.Tests` | `Microsoft.Extensions.DependencyInjection` | 10.0.15 | Resolves the registration in the wiring test (`TS-005`) |
| `Minioasis.Koha.Tests` | `Microsoft.Extensions.Configuration` | 10.0.15 | In-memory configuration for option tests |
| `Minioasis.Koha.Tests` | `Microsoft.Extensions.TimeProvider.Testing` | 10.11.1 | `FakeTimeProvider`, so token expiry and the deadline are tested without waiting (R17, R21, `TS-007`) |
| `Minioasis.Api.Tests` | `Microsoft.AspNetCore.Mvc.Testing` | 10.0.15 | The shared in-process test host (`TS-009`) |
| `Minioasis.Application.Tests` | none | | |

The NSwag generator is already pinned (`dotnet-tools.json`, 14.7.1). Its client uses `System.Text.Json`, so it adds no package.

## Files

Paths are relative to the repository root. Every handwritten type has its own file (`AR-010`).

| Project | File | New or changed | Purpose |
| --- | --- | --- | --- |
| `Minioasis.Application` | `Shared/Errors/ApplicationErrorCategory.cs` | new | The seven categories of R1 |
| | `Shared/Errors/ApplicationErrorDefinition.cs` | new | Code and category; `IsClientVisible` for validation, not found, method not allowed |
| | `Shared/Errors/ApplicationError.cs` | new | A definition with its metadata (placeholder values, and Koha's status for logging) |
| | `Shared/Errors/ErrorDefinitionsAttribute.cs` | new | Marks a definition group |
| | `Shared/Errors/ApplicationErrorDefinitionRegistry.cs` | new | Collects every definition and rejects blank, duplicate, null, undefined category, empty (R9) |
| | `Shared/Errors/SystemErrorDefinitions.cs` | new | `system.unexpected` |
| | `Shared/Errors/RequestErrorDefinitions.cs` | new | `not_found`, `method_not_allowed`, `request.invalid` |
| | `Shared/Errors/DependencyErrorDefinitions.cs` | new | `koha.authentication_failed`, `koha.unavailable`, `koha.invalid_response`, and the selection from `DependencyFailureOutcome` |
| | `Shared/Results/ApplicationResult.cs` | new | Success with a response, or failure with an `ApplicationError` (`AR-012`) |
| | `Shared/Results/DependencyFailureOutcome.cs` | new | Authentication failed, unavailable, invalid response (R20) |
| | `Shared/Extensions/ApplicationServiceCollectionExtensions.cs` | new | `AddApplication()` |
| `Minioasis.Koha` | `Shared/Options/KohaOptions.cs` | new | Base URL, client id, secret, timeout |
| | `Shared/Factories/KohaOptionsFactory.cs` | new | Reads and checks the four variables; messages name the variable, never the value (R10 to R13) |
| | `Shared/Exceptions/KohaConfigurationException.cs` | new | The startup failure of R10 to R12 |
| | `Shared/Exceptions/KohaUnavailableException.cs` | new | Documented: Koha could not be reached at the token step, or the deadline expired |
| | `Authentication/Exceptions/KohaAuthenticationException.cs` | new | Documented: the token request was refused or its reply failed R16 |
| | `Shared/Clients/KohaClientNames.cs` | new | `OAuth` and `Api` |
| | `Authentication/Providers/KohaAccessTokenProvider.cs` | new | R15 to R18: validates, caches with a 30-second margin, one shared request with a count of waiting callers, `Invalidate(token)` |
| | `Authentication/Handlers/KohaBearerHandler.cs` | new | R19: attaches the token, one retry of a `GET` after 401, discards the token after any 401 |
| | `Shared/Handlers/KohaDeadlineHandler.cs` | new | R21: one linked deadline from `TimeProvider`, buffers the body inside it, and turns its own expiry into `KohaUnavailableException` (not a caller cancellation, `ER-009`) |
| | `Shared/Mappers/KohaProtocolFailureMapper.cs` | new | R20 as one table: a status or a caught exception to `DependencyFailureOutcome` (`ER-010`); generated-client exceptions stay inside Koha (`AR-007`) |
| | `Shared/Extensions/KohaServiceCollectionExtensions.cs` | new | `AddKohaIntegration(configuration, allowInsecureHttp)`: options read eagerly, so a bad value fails startup; the two named clients with infinite `HttpClient.Timeout`; the API client's handlers in the order deadline, then bearer; header logging redacted on both |
| | `Generated/OAuth/GeneratedKohaOAuthClient.cs`, `Generated/OAuth/GeneratedKohaOAuthContracts.cs` | new, generated | From `contracts/oauth.openapi.json` (`EX-003`) |
| `Minioasis.Api` | `Program.cs` | changed | Calls the three registration methods, the error handling, and the endpoint mapping |
| | `Resources/errors.en.json` | new | The three framework messages |
| | `Shared/Providers/ErrorMessageProvider.cs` | new | Loads the embedded file, checks it against the registry at startup (R9), fills placeholders and rejects a missing or extra value (R2) |
| | `Shared/Mappers/ApplicationResultHttpMapper.cs` | new | Category to status (R1), and the R5 or R5a log entry |
| | `Shared/Factories/ProblemResponseFactory.cs` | new | The only builder of error bodies: client-visible (R2) and masked (R3); `traceId` from the request's activity (R4) |
| | `Shared/Handlers/ApiExceptionHandler.cs` | new | Unhandled exception: one `Error` entry and a masked 500; `BadHttpRequestException` becomes `request.invalid`; a caller's abort is neither logged nor answered (R22) |
| | `Shared/Handlers/StatusCodeProblemHandler.cs` | new | Empty-bodied 404, 405, 400, 413, 415 from the framework become R6 to R8a (415 and 413 become 400) |
| | `Shared/Abstractions/IEndpointModule.cs` | new | Endpoint registration through dependency injection |
| | `Shared/Extensions/ApiErrorHandlingExtensions.cs` | new | Registers the above, suppresses ASP.NET's own `Error` entry for a handled exception (`ExceptionHandlerOptions.SuppressDiagnosticsCallback`, R5) |
| | `Shared/Extensions/EndpointModuleExtensions.cs` | new | Maps every `IEndpointModule` |
| Tests | `Minioasis.Application.Tests/Shared/Errors/*Tests.cs` | new | Registry and selection tests |
| | `Minioasis.Koha.Tests/Shared/TestDoubles/StubHttpMessageHandler.cs` | new | Scripted replies, recorded requests, a reply that waits for cancellation, a body that stalls or breaks off |
| | `Minioasis.Koha.Tests/Shared/TestDoubles/RecordingLoggerProvider.cs` | new | Captures every category at every level |
| | `Minioasis.Koha.Tests/...` | new | Mirrors the production folders (`TS-011`) |
| | `Minioasis.Api.Tests/Shared/Factories/ApiTestHostFactory.cs` | new | The one test host: dummy Koha settings, per-host settings, environment name, service overrides (`TS-009`) |
| | `Minioasis.Api.Tests/Shared/TestDoubles/TestEndpointModule.cs`, `RecordingLoggerProvider.cs`, `FakeApplicationResultEndpoint.cs` | new | Test routes for R1 to R8a, and log capture |
| Docs | `docs/running.md` | changed | The generation command, and the four variables |
| | `docs/specs/001-errors-and-koha-connection/verification.md` | new | Clause to test, kept current from the first pull request |

## Contracts and generation

`contracts/oauth.openapi.json` copies `POST /oauth/token` (`tokenOAuth`) from `contracts/official_koha_openapi.json` faithfully, with its `error` schema and `servers: [{ "url": "/api/v1" }]` (`EX-002`). The configured base URL already ends in `/api/v1`, so the generated client's `BaseUrl` is the configured base and the token URL is `<base>/oauth/token`.

Generation (written into `docs/running.md`):

```bash
dotnet tool restore
dotnet nswag run nswag.json /variables:Contract=oauth,Feature=OAuth,FeatureSingular=OAuth
```

The generated diff is inspected and committed with the contract (`EX-003`).

## Error definitions

| Code | Category | Client-visible resource entry |
| --- | --- | --- |
| `not_found` | not found | "Not found" / "No resource exists at this address." |
| `method_not_allowed` | method not allowed | "Method not allowed" / "This address does not support this request method." |
| `request.invalid` | validation | "Invalid request" / "The request could not be read." |
| `system.unexpected` | unexpected | none (masked) |
| `koha.authentication_failed` | authentication to a dependency failed | none (masked) |
| `koha.unavailable` | dependency unavailable | none (masked) |
| `koha.invalid_response` | dependency returned an unusable response | none (masked) |

## Tests

Every test is written first and seen to fail (`TS-001`). Log assertions check level, category, and the presence of codes and identifiers, never wording (`TS-010`). Tests that wait use `FakeTimeProvider` or a cancellation token with a test timeout, so a broken behavior fails instead of hanging (`TS-007`). No test reaches a real Koha (`TS-008`).

| Requirement | Layer | Planned test | Fails when |
| --- | --- | --- | --- |
| R1 | Api | `ApplicationResultHttpMapperTests::Maps_each_category_to_its_status` (theory, 7 rows) | A category maps to another status, or one is missing |
| R2 | Api | `ErrorResponseTests::Client_visible_error_has_exact_members_and_content_type`; `ErrorMessageProviderTests::Fills_zero_one_and_two_placeholders`; `::Missing_value_throws`; `::Extra_value_throws`; `ErrorResponseTests::Placeholder_mismatch_is_answered_as_masked_500` | A member is added or missing, `type`/`instance` present, wrong content type, a placeholder is left or ignored |
| R3 | Api | `ErrorResponseTests::Server_failure_is_masked` (theory: 3 server categories, unexpected, thrown exception × `Development`, `Production`) | Any member other than the four, a `detail`, or a different title or code |
| R4 | Api | `TraceIdTests::Response_trace_id_is_w3c_and_matches_the_log` (client-visible, server failure, exception); `::Two_requests_get_different_ids` | Format differs, log and response differ, ids repeat |
| R5 | Api | `ErrorLoggingTests::Server_failure_logs_one_error_with_code_and_trace_id` (each category, with Koha status in metadata); `::Exception_logs_one_error_across_all_categories` | Zero or two `Error` entries, code, status, or `traceId` missing, ASP.NET's own entry not suppressed |
| R5a | Api | `ErrorLoggingTests::Client_visible_error_logs_one_information_entry` (four codes); `::Client_visible_error_logs_nothing_at_warning_or_above_in_any_category` | Wrong level, count, or a warning from any category |
| R6 | Api | `FrameworkErrorTests::Unknown_path_is_not_found` (unknown, under known prefix, `/`) | Empty body, other code |
| R7 | Api | `FrameworkErrorTests::Wrong_method_is_405_with_allow_header` (one-method and two-method routes, `HEAD`, `OPTIONS`) | Status, code, or `Allow` differ |
| R8 | Api | `FrameworkErrorTests::Unreadable_body_is_request_invalid` (theory: malformed, wrong type, empty, no content type, `text/plain`, over size limit); `::Accepted_json_media_types_reach_the_endpoint` (`charset`, upper case, `+json`) | 415 or 413 leaks, an accepted type is refused |
| R8a | Api | `FrameworkErrorTests::Unconvertible_or_missing_parameter_is_request_invalid`; `::Route_constraint_mismatch_is_not_found` | Empty 400, wrong code |
| R9 | Api, Application | `ApplicationErrorDefinitionRegistryTests` (blank, duplicate, null, undefined category, empty); `ErrorMessageProviderTests::Startup_check_rejects` (theory: no message, message for server failure, unknown code, duplicate key, invalid JSON, null, non-object, missing or blank `title`/`detail`); `StartupTests::Real_registry_and_resource_file_are_consistent` | An inconsistency starts anyway, or the message lacks the code |
| R10 | Koha | `KohaOptionsFactoryTests::Missing_or_blank_variable_fails` (3 variables × absent, empty, whitespace) | Startup proceeds, or the name is missing |
| R11 | Koha | `KohaOptionsFactoryTests::Base_url_rules` (theory over every listed input × environment) | A listed case is accepted or refused wrongly; trailing slash changes the base |
| R12 | Koha | `KohaOptionsFactoryTests::Timeout_rules` (theory over the 16 listed values) | A value is accepted or refused wrongly, default is not 10 |
| R13 | Koha | `KohaOptionsFactoryTests::Message_never_contains_the_value` (secret, URL, timeout) | A value appears in the message |
| R14 | Api | `StartupTests::Starting_sends_nothing_to_koha` (Koha clients replaced by a recording handler) | Any request is recorded |
| R15 | Koha | `KohaAccessTokenProviderTests::Sends_one_client_credentials_form_post` (path, method, form fields, no `Authorization`) | Path, fields, or headers differ |
| R16 | Koha | `KohaAccessTokenProviderTests::Accepts_or_rejects_token_reply` (theory over every listed reply) | An invalid reply is accepted or a valid one refused |
| R17 | Koha | `KohaAccessTokenProviderTests::Reuses_until_margin`; `::Refreshes_after_margin`; `::Short_lived_token_is_used_once_and_not_cached` | A second request within the lifetime, none after it, a short token cached |
| R18 | Koha | `KohaAccessTokenProviderTests::Ten_simultaneous_callers_share_one_request` (success; failure then one more); `::One_caller_cancelling_leaves_the_others`; `::All_callers_cancelling_cancels_and_caches_nothing`; `::Shared_request_has_its_own_deadline` | More than one request, a failure cached, the request survives with no waiters |
| R19 | Koha | `KohaBearerHandlerTests` (attaches header; `GET` 401→200; 401→401; 401 after another refresh keeps the newer token; `POST`/`PUT`/`PATCH`/`DELETE` 401 not resent but token discarded; `GET` 500 not retried) | A second retry, a non-`GET` resent, the wrong token discarded |
| R20 | Koha | `KohaProtocolFailureMapperTests::Maps_statuses` (all listed boundaries); `::Maps_exceptions` (network, deadline, cut-off body → unavailable; parse or contract mismatch → invalid response; token refusal → authentication failed); `KohaApiClientPipelineTests::Token_step_failures_by_row` (statuses 400, 401, 403, 429, 500, 599, network, deadline, retry-time token failure); Application `DependencyErrorDefinitionsTests::Selects_definition_and_category`; Api `ErrorLoggingTests` (Koha status logged, R5) | A status or cause lands in another row |
| R21 | Koha | `KohaDeadlineHandlerTests` (expiry during token, headers, slow body, retry) → `KohaUnavailableException` | A slow step outlives the deadline, or expiry surfaces as caller cancellation |
| R22 | Koha, Api | `KohaDeadlineHandlerTests::Caller_cancellation_propagates` (token wait, shared token, call, body, retry; coinciding with expiry); `ApiErrorHandlingTests::Aborted_request_logs_no_error` | Cancellation becomes a failure or an `Error` entry |
| R23 | Koha, Api | `KohaLoggingTests::No_entry_contains_secret_token_header_or_body` (success, token error body, 401 retry, Koha error body with marker; capture all categories at `Trace`) | Any entry contains the token, secret, `Authorization` value, or marker |
| Wiring | Koha, Api | `KohaServiceCollectionExtensionsTests::Registers_clients_and_handler_order` (OAuth client has no bearer handler; API client order deadline then bearer); `ApplicationServiceCollectionExtensionsTests::Resolves_registry` (`TS-005`) | A handler missing or out of order |

Covered edge cases and prohibitions map to the same tests: placeholder mismatch (R2 tests), `HEAD`/`OPTIONS` (R7), sign/zeros/spaces/overflow (R12), query/fragment/user info (R11), token-endpoint 400/403 (R20 pipeline), token expiring in flight (R19), refresh after a retry (R18, R19), empty 2xx body (R20 mapper), slow body (R21), first call while Koha is down (R20 pipeline, network at token step), 413 and unconvertible values (R8, R8a); and every prohibition by the requirement its row names.

## Pull request breakdown

All coding goes to `coder-heavy`: it touches the error contract, authentication, the Koha integration, and cancellation (`CLAUDE.md`, section 10). Each pull request writes its tests first and updates `verification.md`.

1. **Application error model.** Packages for Application; categories, definitions, registry, results, `DependencyFailureOutcome`, the three definition groups. R1 (categories), R9 (registry part), R20 (selection).
2. **Api error contract.** Packages for Api tests; mapper, factory, message provider and startup check, exception handler, framework errors, endpoint modules, test host, resource file. R1 to R9, R5a, R22 (Api part).
3. **Koha configuration and generated client.** Packages for Koha; options and their startup check, registration, focused contract and generated client, `docs/running.md`. R10 to R14, wiring.
4. **Token provider.** R15 to R18, R23 (token part).
5. **Handlers and failure mapping.** Bearer handler, deadline handler, failure mapper. R19 to R23.

## Risks

- **`traceId` source.** ASP.NET creates the request activity only when something listens. Mitigation: the R4 tests run on the real host, and the factory fails loudly (a test failure, not a silent fallback) if no activity exists; the coder adds the minimal listener if needed.
- **Framework errors that bypass status-code pages** (a non-empty 400 written by the framework). Mitigation: the R8 theory covers each listed class on the real host.
- **The shared token request and cancellation** (R18) is the most concurrency-sensitive code. Mitigation: deterministic tests with `FakeTimeProvider` and handler doubles that block until released; no `Task.Delay` in tests.
- **Shared code before its first user.** `KohaProtocolFailureMapper`, `DependencyErrorDefinitions`, and `IEndpointModule` have no production caller until spec 003. The spec requires them (D8); `CD-007` is not broken because tests reach every branch and spec 003 is their first user.
- **Generated client behavior** on a reply that is not JSON or has an unexpected status. Mitigation: R16 and R20 tests go through the generated client, not around it.

## Plan check

<To be completed by the reviewer.>

## Deviations

None.
