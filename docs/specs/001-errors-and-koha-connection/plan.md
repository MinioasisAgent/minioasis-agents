- **Access:** Level 2
- **Last Updated:** 2026-11-20

# 001 — API errors and Koha connection: implementation plan

- **Spec:** [spec.md](spec.md)
- **Plan status:** approved

## Approach

The work fills the three projects of `AR-001`; no new project or layer.

- **`Minioasis.Application`** owns the error model: the categories, the definitions, the registry of all definitions, and the result type (`ER-001`, `ER-002`). It also owns `DependencyFailureOutcome`, the three shared Koha failure outcomes of R20, so that the Koha project can report them in Application's own types (`AR-006`).
- **`Minioasis.Api`** owns everything the client sees: the category-to-status mapper (R1), the one factory that builds every error body (R2, R3), the English resource file and its startup check (R9), the exception handler (R3, R5), the shaping of framework errors (R6 to R8a), and the trace identifier (R4). It is the composition root (`AR-005`).
- **`Minioasis.Koha`** owns the connection: configuration and its startup check (R10 to R13), the generated OAuth client, the token provider (R15 to R18), the bearer handler (R19), the deadline handler (R21), and the mapper from HTTP results to `DependencyFailureOutcome` (R20). Capabilities call Koha through the named client `KohaClientNames.Api` and pass the caller's token on (`EX-004`, `EX-012`).

How a failure travels: a capability's adapter (from spec 003 on) catches the documented Koha exceptions and HTTP statuses at its boundary, asks `KohaProtocolFailureMapper` for the shared outcome unless it claims the status itself (D8), and returns a gateway result. The adapter reads Koha's status (from the generated exception at the call step, or from the Koha exception's `KohaStatus` at the token step) and returns it in the gateway result. The use case selects the definition from `DependencyErrorDefinitions` and copies the status into the error's `DependencyStatus`. Api maps the category to the status, writes the masked body, and writes the single `Error` entry with the internal code, Koha's status, and the `traceId` (R5, R20). The Koha project logs nothing at `Warning` or above.

**Choices made under "Agent's discretion":**

- **Refresh margin:** 30 seconds.
- **Error registry:** built by reflection over the public static `ApplicationErrorDefinition` fields of static classes marked `[ErrorDefinitions]` in the Application assembly, so a new definition cannot be left out of the startup check. A null field is reported by the check (R9).
- **Framework error definitions** live in `Minioasis.Application/Shared/Errors/RequestErrorDefinitions.cs`: `not_found`, `method_not_allowed`, `request.invalid`.
- **Resource file:** `Minioasis.Api/Resources/errors.en.json`, embedded in the assembly, so that publish and tests read the same bytes.
- **Endpoint registration:** Api maps every `IEndpointModule` registered in dependency injection. Today no module exists in production. Tests register a test module to reach R1 to R8a, and the first capability adds the first real one. This is the smallest way to give the test host endpoints without a second host (`TS-009`).
- **The `Development` check:** Api passes `builder.Environment.IsDevelopment()` to `AddKohaIntegration`, so the Koha project needs no hosting package.
- **No interface for the token provider:** only the bearer handler uses it, and tests reach it through `HttpMessageHandler` doubles (`CD-008`, `TS-004`). Its documented exceptions are its contract (`CD-005`).
- **Internal codes:** exactly as the spec's "Errors" table spells them.
- **Koha's status in an error:** a separate `DependencyStatus` property on `ApplicationError`, never in the placeholder values, so it cannot trip R2's extra-value check and is never shown (`AR-003`).
- **Unreadable requests take one path.** `RouteHandlerOptions.ThrowOnBadRequest` is set to `false` in every environment, so binding failures always end as empty 400 or 415 responses that `StatusCodeProblemHandler` shapes, never as exceptions. The exception handler therefore has no `BadHttpRequestException` branch (`CD-007`). The body-size limit is Kestrel's default `MaxRequestBodySize`, unchanged; Kestrel's over-size failure is caught by ASP.NET's request delegate and answered as an empty 413, which the same handler turns into 400.
- **Trace identifier with or without logging.** Api registers an `ActivityListener` for the `Microsoft.AspNetCore` activity source with `Sample = PropagationData`, so every request has a W3C activity even when logging is switched off (R4).
- **One place writes and logs every error.** `ErrorResponseWriter` is the only component that writes an error response and the only one that writes the R5 and R5a entries. The mapper, the exception handler, and the status-code handler all call it (`CD-006`).
- **Startup checks run before the first request.** Koha options are read inside `AddKohaIntegration`, before `Build()`. The error-message check runs in `Program.cs` after `Build()` and before `Run()`, by resolving the message catalog. The catalog's source (the embedded file) and the registry's definitions are each registered as a service, so a test host can replace them (R9).
- **The deadline handler on both clients.** `KohaDeadlineHandler` has one implementation, registered by one method, `AddKohaIntegration`, on both named clients: on the API client it bounds the whole call (R21); on the OAuth client it bounds the shared token request from the moment it is sent, independently of any caller (R18). Its expiry is `KohaUnavailableException` on both, which is the token-step and call-step 503 of R20. This is one implementation and one registration point (`CD-006`), not two deadlines for the same work: the caller's wait and the shared request are different pieces of work that R18 bounds separately.
- **Exception contract of the Koha clients**, documented once, on `KohaClientNames`: a send through the API client completes, or throws `KohaAuthenticationException`, `KohaUnavailableException`, `HttpRequestException` (a body that breaks off arrives as an `IOException` inside one), or the caller's `OperationCanceledException` (`CD-005`). The two Koha exceptions carry no inner generated exception and no Koha body (R23).
- **Generated-client failures are classified by status.** Each generated client has its own `KohaServiceException` type, so `KohaProtocolFailureMapper` takes the status the adapter read from it: any 2xx in a generated exception (a body that cannot be parsed or does not match) is invalid response; other statuses follow R20's table.
- **The generated client's `BaseUrl`** is assigned from `KohaOptions.BaseUrl` by the token provider; the generated default (`/api/v1`) is never relied on.
- **`TimeProvider.System`** is registered by `AddKohaIntegration` unless one is already registered, so tests can supply `FakeTimeProvider`.
- **Unhandled exceptions take one path.** `UseExceptionHandler` is configured with `ExceptionHandlerOptions.ExceptionHandler`, a delegate that writes the masked 500 and its single `Error` entry through `ErrorResponseWriter`, and `SuppressDiagnosticsCallback = _ => true`, so ASP.NET writes no `Error` entry of its own. There is no `IExceptionHandler`, so no branch is unreachable (`CD-007`). A caller's abort never reaches the delegate: ASP.NET's middleware handles it itself (logs at `Debug`, no response). `AddProblemDetails()` is not used, so ASP.NET adds no `type` member (`ER-005`).
- **No automatic redirects.** Both named Koha clients use `SocketsHttpHandler { AllowAutoRedirect = false }` as their primary handler, so a 3xx is a final reply (R20), no request is resent to another location (R19, `EX-010`), and the token request is sent once and never carries the secret elsewhere (R15).
- **`HEAD` responses.** `ErrorResponseWriter` sets the status, content type, and headers such as `Allow`, and writes no body for a `HEAD` request, for every error status. This depends on a wording change to R2 and R7 that the maintainer accepts separately (see "Plan check"); if it is not accepted, the writer writes the body for `HEAD` as the spec now says, and Kestrel's own `HEAD` handling is unchanged.
- **Koha's status at the token step.** `KohaAuthenticationException` and `KohaUnavailableException` carry a nullable `KohaStatus`: Koha's HTTP status when Koha replied (including 200 for a reply that fails R16), null for a network error or the deadline. It is a number, so it is safe under R23.
- **The request activity listener** is created and disposed with the host (a hosted service owns it), so one test host's listener cannot make another host's test pass.

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
| `MINIOASIS_KOHA_BASE_URL`, `_CLIENT_ID`, `_CLIENT_SECRET` names | Every deployment's configuration | R10, `EX-005`, ADR 0003 |
| Sign-in to Koha: client-credentials form `POST` to `<base>/oauth/token` | The Koha API client of the service account | R15, `EX-006` |
| `Allow` header on 405 | Clients that read it | R7, D2 |
| 413 and 415 answered as 400 `request.invalid` | Clients that branch on 413 or 415 | R8, D2 |

## Dependencies

Each is a new dependency (Level 2, item 4), accepted with this plan. Versions match the installed .NET runtime, 10.0.15. If a version cannot be restored, the coder stops and reports it (`docs/workflow/implementation.md`).

| Project | Package | Version | Why |
| --- | --- | --- | --- |
| `Minioasis.Application` | `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.15 | `AddApplication()` registers the registry (`AR-005`) |
| `Minioasis.Koha` | `Microsoft.Extensions.Http` | 10.0.15 | `IHttpClientFactory`, named clients, handler pipeline (`EX-007`) |
| `Minioasis.Koha` | `Microsoft.Extensions.Configuration.Abstractions` | 10.0.15 | Reads the four variables (`EX-005`) |
| `Minioasis.Koha.Tests` | `Microsoft.Extensions.DependencyInjection` | 10.0.15 | Resolves the registration in the wiring test (`TS-005`) |
| `Minioasis.Koha.Tests` | `Microsoft.Extensions.Configuration` | 10.0.15 | In-memory configuration for option tests |
| `Minioasis.Koha.Tests` | `Microsoft.Extensions.Logging` | 10.0.15 | `AddLogging` and the recording logger provider |
| `Minioasis.Koha.Tests` | `Microsoft.Extensions.TimeProvider.Testing` | 10.11.1 | `FakeTimeProvider`, so token expiry and the deadline are tested without waiting (R17, R21, `TS-007`) |
| `Minioasis.Api.Tests` | `Microsoft.AspNetCore.Mvc.Testing` | 10.0.15 | The shared in-process test host (`TS-009`) |
| `Minioasis.Application.Tests` | `Microsoft.Extensions.DependencyInjection` | 10.0.15 | Resolves `AddApplication()` in the wiring test (`TS-005`) |

`Microsoft.Extensions.Configuration.Abstractions` (Koha) and `Microsoft.Extensions.DependencyInjection` (Koha.Tests) also arrive through `Microsoft.Extensions.Http`; they are referenced directly on purpose, because the code uses them directly. The NSwag generator is already pinned (`dotnet-tools.json`, 14.7.1). Its client uses `System.Text.Json`, so it adds no package.

## Files

Paths are relative to the repository root. Every handwritten type has its own file (`AR-010`).

| Project | File | New or changed | Purpose |
| --- | --- | --- | --- |
| `Minioasis.Application` | `Shared/Errors/ApplicationErrorCategory.cs` | new | The seven categories of R1 |
| | `Shared/Errors/ApplicationErrorDefinition.cs` | new | Code and category; `IsClientVisible` for validation, not found, method not allowed |
| | `Shared/Errors/ApplicationError.cs` | new | A definition, its placeholder values, and a separate `DependencyStatus` for logging |
| | `Shared/Errors/ErrorDefinitionsAttribute.cs` | new | Marks a definition group |
| | `Shared/Errors/ApplicationErrorDefinitionRegistry.cs` | new | Collects every definition and rejects blank, duplicate, null, undefined category, empty (R9) |
| | `Shared/Errors/SystemErrorDefinitions.cs` | new | `system.unexpected` |
| | `Shared/Errors/RequestErrorDefinitions.cs` | new | `not_found`, `method_not_allowed`, `request.invalid` |
| | `Shared/Errors/DependencyErrorDefinitions.cs` | new | `koha.authentication_failed`, `koha.unavailable`, `koha.invalid_response`, and the selection from `DependencyFailureOutcome` |
| | `Shared/Results/ApplicationResult.cs` | new | Success with a response, or failure with an `ApplicationError` (`AR-012`) |
| | `Shared/Outcomes/DependencyFailureOutcome.cs` | new | Authentication failed, unavailable, invalid response (R20) |
| | `Shared/Errors/ApplicationErrorDefinitionSource.cs` | new | The definitions the registry checks: by default the `[ErrorDefinitions]` scan; replaceable in tests (R9) |
| | `Minioasis.Application.csproj` | changed | The DI abstractions package |
| | `Shared/Extensions/ApplicationServiceCollectionExtensions.cs` | new | `AddApplication()` |
| `Minioasis.Koha` | `Minioasis.Koha.csproj` | changed | The two packages |
| | `Shared/Options/KohaOptions.cs` | new | Base URL, client id, secret, timeout |
| | `Shared/Factories/KohaOptionsFactory.cs` | new | Reads and checks the four variables; messages name the variable, never the value (R10 to R13) |
| | `Shared/Exceptions/KohaConfigurationException.cs` | new | The startup failure of R10 to R12 |
| | `Shared/Exceptions/KohaUnavailableException.cs` | new | Documented: Koha could not be reached at the token step, or the deadline expired; nullable `KohaStatus` |
| | `Authentication/Exceptions/KohaAuthenticationException.cs` | new | Documented: the token request was refused or its reply failed R16; nullable `KohaStatus` |
| | `Shared/Clients/KohaClientNames.cs` | new | `OAuth` and `Api`, with the documented exception contract of a send |
| | `Authentication/Providers/KohaAccessTokenProvider.cs` | new | R15 to R18: validates, caches with a 30-second margin, one shared request with a count of waiting callers, `Invalidate(token)` |
| | `Authentication/Handlers/KohaBearerHandler.cs` | new | R19: attaches the token, one retry of a `GET` after 401, discards the token after any 401 |
| | `Shared/Handlers/KohaDeadlineHandler.cs` | new | R21: one linked deadline from `TimeProvider`, buffers the body inside it, and turns its own expiry into `KohaUnavailableException` (not a caller cancellation, `ER-009`) |
| | `Shared/Mappers/KohaProtocolFailureMapper.cs` | new | R20 as one table: a status or a caught exception to `DependencyFailureOutcome` (`ER-010`); generated-client exceptions stay inside Koha (`AR-007`) |
| | `Shared/Extensions/KohaServiceCollectionExtensions.cs` | new | `AddKohaIntegration(configuration, allowInsecureHttp)`: options read eagerly, so a bad value fails startup; `TimeProvider.System`; the two named clients with infinite `HttpClient.Timeout`; the OAuth client with the deadline handler only; the API client with deadline, then bearer; header logging redacted on both; both clients' primary handler `SocketsHttpHandler { AllowAutoRedirect = false }` |
| | `Generated/OAuth/GeneratedKohaOAuthClient.cs`, `Generated/OAuth/GeneratedKohaOAuthContracts.cs` | new, generated | From `contracts/oauth.openapi.json` (`EX-003`) |
| Contracts | `contracts/oauth.openapi.json` | new | Focused contract for `tokenOAuth` |
| `Minioasis.Api` | `Program.cs` | changed | Calls the three registration methods, the error handling, the endpoint mapping, and the message check between `Build()` and `Run()` |
| | `Minioasis.Api.csproj` | changed | `errors.en.json` as `EmbeddedResource`, removed from Web SDK content |
| | `Resources/errors.en.json` | new | The three framework messages |
| | `Shared/Factories/ErrorMessageCatalogFactory.cs` | new | Loads the message source and checks it against the registry (R9) |
| | `Shared/Models/ErrorMessageSource.cs` | new | The bytes of the message file: by default the embedded resource; replaceable in tests |
| | `Shared/Providers/ErrorMessageProvider.cs` | new | Fills placeholders from the checked catalog and rejects a missing or extra value (R2) |
| | `Shared/Mappers/ApplicationResultHttpMapper.cs` | new | Category to status (R1), then hands the error to `ErrorResponseWriter` |
| | `Shared/Writers/ErrorResponseWriter.cs` | new | The one writer and logger of every error response: R5 and R5a entries, with code, `DependencyStatus`, and `traceId`; no body for `HEAD` |
| | `Shared/Factories/ProblemResponseFactory.cs` | new | The only builder of error bodies: client-visible (R2) and masked (R3); `traceId` from the request's activity (R4) |
| | `Shared/Extensions/RequestActivityExtensions.cs`, `Shared/Hosting/RequestActivityListenerHostedService.cs` | new | Registers the `ActivityListener` (R4) through a hosted service that disposes it when the host stops |
| | `Shared/Models/ErrorMessageCatalog.cs` | new | The checked messages, keyed by code, built by `ErrorMessageCatalogFactory` |
| | `Shared/Handlers/StatusCodeProblemHandler.cs` | new | Empty-bodied 404, 405, 400, 413, 415 from the framework become R6 to R8a (415 and 413 become 400); every other status is left unchanged |
| | `Shared/Abstractions/IEndpointModule.cs` | new | Endpoint registration through dependency injection |
| | `Shared/Extensions/ApiErrorHandlingExtensions.cs` | new | Registers the above, sets `ThrowOnBadRequest = false`, and configures the exception handler middleware (delegate and suppression, see "Approach") and status-code pages |
| | `Shared/Extensions/EndpointModuleExtensions.cs` | new | Maps every `IEndpointModule` |
| Tests | `Minioasis.Application.Tests/Shared/Errors/ApplicationErrorDefinitionRegistryTests.cs`, `DependencyErrorDefinitionsTests.cs`; `Shared/Extensions/ApplicationServiceCollectionExtensionsTests.cs` | new | Registry, selection, and wiring tests |
| | `Minioasis.Koha.Tests/Shared/Extensions/KohaServiceCollectionExtensionsTests.cs`, `KohaApiClientPipelineTests.cs`, `KohaLoggingTests.cs`; `Shared/Handlers/KohaDeadlineHandlerTests.cs`; `Shared/Mappers/KohaProtocolFailureMapperTests.cs`; `Shared/Factories/KohaOptionsFactoryTests.cs`; `Authentication/Providers/KohaAccessTokenProviderTests.cs`; `Authentication/Handlers/KohaBearerHandlerTests.cs` | new | The Koha test classes, mirroring production; the pipeline and logging tests are built from `AddKohaIntegration` |
| | `Minioasis.Koha.Tests/Shared/TestDoubles/StubHttpMessageHandler.cs` | new | Scripted replies, recorded requests, a reply that waits for cancellation, a body that stalls or breaks off |
| | `Minioasis.Koha.Tests/Shared/TestDoubles/RecordingLoggerProvider.cs` | new | Captures every category at every level |
| | `Minioasis.Koha.Tests/...` | new | Mirrors the production folders (`TS-011`) |
| | `Minioasis.Api.Tests/Shared/Factories/ApiTestHostFactory.cs` | new | The one test host: dummy Koha settings, per-host settings applied with `UseSetting` so they are visible before `AddKohaIntegration` reads them, environment name, logging on or off, service overrides (`TS-009`) |
| | `Minioasis.Api.Tests/Shared/TestDoubles/TestEndpointModule.cs`, `RecordingLoggerProvider.cs`, `OversizedBodyStream.cs`, `OversizedBodyStartupFilter.cs`, `TestErrorDefinitions.cs` | new | Test routes for R1 to R8a; test definitions and messages (one with two placeholders, one client-visible `test.not_found`) swapped in through `ApplicationErrorDefinitionSource` and `ErrorMessageSource`; log capture; on the server side, for requests marked by a test header, an `IStartupFilter` replaces `HttpContext.Request.Body` with a stream that throws `BadHttpRequestException` with status 413 partway through, as Kestrel does, so ASP.NET's request delegate answers an empty 413 (R8) |
| | `Minioasis.Api.Tests/Shared/Mappers/ApplicationResultHttpMapperTests.cs`; `Shared/Writers/ErrorResponseTests.cs`, `ErrorLoggingTests.cs`; `Shared/Hosting/TraceIdTests.cs`; `Shared/Providers/ErrorMessageProviderTests.cs`; `Shared/Factories/ErrorMessageCatalogFactoryTests.cs`; `Shared/Handlers/FrameworkErrorTests.cs`; `Shared/Extensions/ApiErrorHandlingTests.cs`; `StartupTests.cs` | new | The Api test classes, mirroring the production folders (`TS-011`) |
| | `Minioasis.*.Tests/*.csproj` | changed | The test packages |
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
| R2 | Api | `ErrorResponseTests::Client_visible_error_has_exact_members_and_content_type`; `ErrorMessageProviderTests::Fills_zero_one_and_two_placeholders`; `::Dependency_status_is_not_a_placeholder_value`; `::Missing_value_throws`; `::Extra_value_throws`; `ErrorResponseTests::Placeholder_mismatch_is_answered_as_masked_500` | A member is added or missing, `type`/`instance` present, wrong content type, a placeholder is left or ignored |
| R3 | Api | `ErrorResponseTests::Server_failure_is_masked` (theory: 3 server categories, unexpected, thrown exception × `Development`, `Production`) | Any member other than the four, a `detail`, or a different title or code |
| R4 | Api | `TraceIdTests::Response_trace_id_is_w3c_and_matches_the_log` (client-visible, server failure, exception); `::Two_requests_get_different_ids`; `::Trace_id_present_when_logging_is_disabled` (no logger providers, both environments; in a non-parallel test collection, so no other host's listener is alive; it first asserts that the `Microsoft.AspNetCore` source has no listener) | Format differs, log and response differ, ids repeat, the activity depends on logging |
| R5 | Api | `ErrorLoggingTests::Server_failure_logs_one_error_with_code_and_trace_id` (each category, with `DependencyStatus` set and unset); `::Exception_logs_one_error_across_all_categories` | Zero or two `Error` entries in any category; code, `DependencyStatus`, or `traceId` missing |
| R5a | Api | `ErrorLoggingTests::Client_visible_error_logs_one_information_entry` (four codes); `::Client_visible_error_logs_nothing_at_warning_or_above_in_any_category` (framework codes in `Development` and `Production`) | Wrong level, count, or a warning from any category |
| R6 | Api | `FrameworkErrorTests::Unknown_path_is_not_found` (unknown, under known prefix, `/`, and `HEAD /does-not-exist` without a body) | Empty body, other code |
| R7 | Api | `FrameworkErrorTests::Wrong_method_is_405_with_allow_header` (one-method and two-method routes, `OPTIONS`: status, `Allow`, body with `code`); `::Head_on_wrong_method_is_405_without_body` (status, `Allow`, `Content-Type: application/problem+json`, empty body, one R5a entry) | Status, `Allow`, or `code` differ; a `HEAD` response has a body |
| R8 | Api | `FrameworkErrorTests::Unreadable_body_is_request_invalid` (theory: malformed, wrong type, empty, no content type, `text/plain`, over size limit through `OversizedBodyStartupFilter`, with a test timeout (`TS-007`); each in `Development` and `Production`); `::Accepted_json_media_types_reach_the_endpoint` (`charset`, upper case, `+json`) | 415 or 413 leaks, a 500 appears, an accepted type is refused, the environments differ |
| R8a | Api | `FrameworkErrorTests::Unconvertible_or_missing_parameter_is_request_invalid` (`Development` and `Production`); `::Route_constraint_mismatch_is_not_found` | Empty 400, a 500, wrong code |
| R9 | Api, Application | `ApplicationErrorDefinitionRegistryTests` (blank, duplicate, null, undefined category, empty); `ErrorMessageCatalogFactoryTests::Startup_check_rejects` (theory: no message, message for server failure, unknown code, duplicate key, invalid JSON, null, non-object, missing or blank `title`/`detail`); `StartupTests::Real_registry_and_resource_file_are_consistent`; `StartupTests::Inconsistent_messages_stop_the_host` (message source replaced) | An inconsistency starts anyway, the check runs lazily, or the message lacks the code |
| R10 | Koha, Api | `KohaOptionsFactoryTests::Missing_or_blank_variable_fails` (3 variables × absent, empty, whitespace); `StartupTests::Invalid_koha_variable_stops_the_host` (blank secret) | Startup proceeds, or the name is missing |
| R11 | Koha, Api | `KohaOptionsFactoryTests::Base_url_rules` (theory over every listed input × environment); `StartupTests::Http_base_url_fails_in_production_and_starts_in_development` | A listed case is accepted or refused wrongly; trailing slash changes the base; `Program.cs` passes the wrong environment flag |
| R12 | Koha | `KohaOptionsFactoryTests::Timeout_rules` (theory over the 16 listed values) | A value is accepted or refused wrongly, default is not 10 |
| R13 | Koha | `KohaOptionsFactoryTests::Message_never_contains_the_value` (secret, URL, timeout; asserts on `exception.ToString()`, inner exceptions included) | A value appears anywhere in the exception |
| R14 | Api | `StartupTests::Starting_sends_nothing_to_koha` (a recording primary handler on every named client; it knows no Koha path or payload, `TS-003`) | Any request is recorded |
| R15 | Koha | `KohaAccessTokenProviderTests::Sends_one_client_credentials_form_post` (method, form fields, no `Authorization`; absolute URL for bases `https://koha.test/koha/api/v1` and `https://koha.test/koha/api/v1/`) | Path, fields, or headers differ; the generated default base is used |
| R16 | Koha | `KohaAccessTokenProviderTests::Accepts_or_rejects_token_reply` (theory over every listed reply) | An invalid reply is accepted or a valid one refused |
| R17 | Koha | `KohaAccessTokenProviderTests::Reuses_until_margin`; `::Refreshes_after_margin`; `::Short_lived_token_is_used_once_and_not_cached`; `::Margin_boundary` (lifetime 30 s not cached, 31 s cached; reuse one tick before expiry minus margin, refresh exactly at it) | A second request within the lifetime, none after it, a short token cached, the boundary off by one |
| R18 | Koha | `KohaAccessTokenProviderTests::Ten_simultaneous_callers_share_one_request` (success; failure then one more); `::One_caller_cancelling_leaves_the_others`; `::All_callers_cancelling_cancels_and_caches_nothing`; `KohaApiClientPipelineTests::Shared_token_request_expires_on_its_own_deadline` (caller A starts; caller B joins half a deadline later; when the shared request's deadline expires, B gets `KohaUnavailableException` before its own deadline, and the stub sees the token request cancelled) | More than one request, a failure cached, the request survives with no waiters, the shared request has no deadline |
| R19 | Koha | `KohaBearerHandlerTests` (attaches header; `GET` 401→200; 401→401; 401 after another refresh keeps the newer token; `POST`/`PUT`/`PATCH`/`DELETE` 401 not resent but token discarded; `GET` 500 not retried); `::Simultaneous_401s_share_one_token_request` (10 `GET`s with 401 on the same token; the token stub holds its reply until all 10 are waiting, then releases it: exactly one new token request) | A second retry, a non-`GET` resent, the wrong token discarded, a refresh per caller |
| R20 | Koha | `KohaProtocolFailureMapperTests::Maps_statuses` (all listed boundaries, plus 300 and 302); `::Maps_exceptions` (network, deadline → unavailable; token refusal → authentication failed); `::Maps_generated_exception_status` (any 2xx → invalid response, others by the table); `KohaApiClientPipelineTests::Body_breaking_off_arrives_as_network_error` (through the deadline handler: an `HttpRequestException` around the `IOException`, mapped to unavailable); `::Token_step_failures_by_row` (statuses 201, 302, 400, 401, 403, 404, 429, 500, 599, network, deadline, retry-time token failure; each asserts the exception's `KohaStatus`); Application `DependencyErrorDefinitionsTests::Selects_definition_and_category`; Api `ErrorLoggingTests` (Koha status logged, R5) | A status or cause lands in another row |
| R21 | Koha | `KohaDeadlineHandlerTests` (expiry while waiting for headers, while reading a slow body); `KohaApiClientPipelineTests::Deadline_covers_every_step` (expiry during the token step and during the retry) → `KohaUnavailableException` | A slow step outlives the deadline, or expiry surfaces as caller cancellation |
| R22 | Koha, Api | `KohaApiClientPipelineTests::Caller_cancellation_propagates` (token wait, shared token, call, body, retry; coinciding with expiry); `ApiErrorHandlingTests::Aborted_request_logs_no_error` | Cancellation becomes a failure or an `Error` entry. The Api test guards ASP.NET's own handling of an aborted request; `verification.md` records it as a framework guard and how it was seen to fail (by adding a temporary `Error` log in the abort path) |
| R23 | Koha, Api | `KohaLoggingTests::No_entry_contains_secret_token_header_or_body` (success, token error body, 401 retry, Koha error body with marker; capture all categories at `Trace`); `::Koha_exceptions_carry_no_body` (`ToString()` of each documented exception lacks the marker; `KohaStatus` is set when Koha replied) | Any entry or exception text contains the token, secret, `Authorization` value, or marker |
| R5 (Koha clause) | Koha | `KohaLoggingTests::Koha_writes_nothing_at_warning_or_above` (token refusal, network, deadline, 5xx, cancellation; all categories at `Trace`) | The Koha project writes its own `Warning` or `Error` entry |
| Wiring | Koha, Application | `KohaServiceCollectionExtensionsTests::Registers_clients_and_handler_order` (OAuth client: deadline handler, no bearer handler; API client: deadline then bearer; both primary handlers have `AllowAutoRedirect = false`); `Minioasis.Application.Tests` `ApplicationServiceCollectionExtensionsTests::Resolves_registry` (`TS-005`) | A handler missing or out of order |

Covered edge cases and prohibitions map to the same tests: placeholder mismatch (R2 tests), `HEAD`/`OPTIONS` (R7), sign/zeros/spaces/overflow (R12), query/fragment/user info (R11), token-endpoint 400/403 (R20 pipeline), token expiring in flight (R19), simultaneous refresh after 401s (`Simultaneous_401s_share_one_token_request`), empty 2xx body (R20 mapper), slow body (R21), first call while Koha is down (R20 pipeline, network at token step), 413 and unconvertible values (R8, R8a); and every prohibition by the requirement its row names.

## Pull request breakdown

All coding goes to `coder-heavy`: it touches the error contract, authentication, the Koha integration, and cancellation (`CLAUDE.md`, section 10). Each pull request writes its tests first and updates `verification.md`.

1. **Application error model.** Packages for Application; categories, definitions, registry, results, `DependencyFailureOutcome`, the three definition groups. R1 (categories), R9 (registry part), R20 (selection).
2. **Api error contract.** Packages for Api tests; mapper, factory, message provider and startup check, exception handling, error writer, activity listener, framework errors, endpoint modules, test host, resource file. R1 to R9, R5a, R22 (Api part).
3. **Koha configuration and generated client.** Packages for Koha; options and their startup check, registration of the clients, focused contract and generated client, `docs/running.md`, and the `Program.cs` call with the environment flag. R10 to R14, including `StartupTests` for R10, R11, and R14; the wiring test for the two clients without handlers.
4. **Token provider.** R15 to R18, R23 (token part).
5. **Handlers and failure mapping.** Bearer handler, deadline handler on both clients, failure mapper, and the handler-order and redirect assertions of the wiring test, and `KohaLoggingTests::Koha_writes_nothing_at_warning_or_above`. R18 (shared deadline), R19 to R23.

## Risks

- **`traceId` source.** ASP.NET creates the request activity only when something listens. Mitigation: Api's own `ActivityListener`, proved by the test with logging switched off.
- **Framework errors that bypass status-code pages.** Mitigation: `ThrowOnBadRequest = false` everywhere, and the R8 and R8a theories in both environments. The test host is `TestServer`, which enforces no body limit, so the over-size case is produced on the server side by `OversizedBodyStartupFilter`, which fails the way Kestrel does (a client-side stream does not reach the server as a 413).
- **The shared token request and cancellation** (R18) is the most concurrency-sensitive code. Mitigation: deterministic tests with `FakeTimeProvider` and handler doubles that block until released; no `Task.Delay` in tests.
- **Shared code before its first user.** `KohaProtocolFailureMapper`, `DependencyErrorDefinitions`, and `IEndpointModule` have no production caller until spec 003. The spec requires them (D8); `CD-007` is not broken because tests reach every branch and spec 003 is their first user.
- **Generated client behavior** on a reply that is not JSON or has an unexpected status. Mitigation: R16 and R20 tests go through the generated client, not around it.

## Plan check

**Round 1** (`reviewer`, 2026-11-20): 9 blocking and 13 non-blocking findings. All addressed in this revision:

- B1 over-size body: `OversizedBodyStream`; Kestrel's default limit stated. B2 two paths for bad requests: `ThrowOnBadRequest = false` everywhere, tests in both environments. B3 trace identifier without logging: `ActivityListener` and a test with logging off. B4 shared token deadline: the deadline handler on the OAuth client, with the `CD-006` reasoning in "Approach". B5 startup wiring: the check's place in `Program.cs`, replaceable sources, three host-level startup tests. B6 simultaneous 401s: a dedicated test. B7 the Koha project's own logging: a dedicated test. B8 missing package: added to Application.Tests. B9 unreachable abort branch: removed; the Api test is recorded as a framework guard.
- N1 bodies in exceptions; N2 generated exceptions by status; N3 base URL test; N4 no suppression setting; N5 exception-handler fallback without `AddProblemDetails`; N6 `UseSetting`; N7 one-way rows; N8 packages stated as intentional; N9 files; N10 handler order moved to PR 5; N11 margin boundary; N12 `ToString()`; N13 `HEAD` bodies: a wording correction to R7 in the spec. Rule-audit notes: `DependencyStatus` field (`AR-003`), `Outcomes` folder (`AR-008`), loading split from formatting and one writer for logging (`CD-001`, `CD-006`), exception contract on `KohaClientNames` (`CD-005`), R14 test free of Koha knowledge (`TS-003`).

**Round 2** (`reviewer`, 2026-11-20): 4 blocking (down from 9) and 11 non-blocking findings. All addressed in this revision: P1 the over-size body is produced on the server by `OversizedBodyStartupFilter`; P2 `HEAD` writes no body, with its own test; P3 automatic redirects off on both clients, with wiring and 3xx tests; P4 `KohaStatus` on both Koha exceptions. N1 `DependencyStatus` wording; N2 missing file rows; N3 one exception path, no `IExceptionHandler`; N4 listener tied to the host, test non-parallel; N5 deterministic 401 test; N6 pipeline tests moved; N7 boundaries; N8 PR contents; N9 `Writers` folder; N10 the R7 change goes to the maintainer; N11 the exception's source cited below.

**Round 3** (`reviewer`, 2026-11-20): no blocking issue; 11 non-blocking findings, all addressed: N1 `HEAD` bodies for every error status, moved with the R7 wording into one separate spec change for the maintainer; N2 a test of a body breaking off through the deadline handler; N3 the shared-deadline test setup; N4 the adapter, not the use case, reads Koha's status; N5 the redirect setting in the file row; N6 test folders; N7 `Hosting` folder; N8 test definitions; N9 the listener precondition; N10 other statuses unchanged; N11 the logging package. Plan status: `checked`, then `approved` under the exception below.

**Exception granted by the maintainer for spec 001 only.** In the project thread for this spec, on 2026-11-20, the maintainer asked to code without their approval; the coordinator pointed out the conflict with `CLAUDE.md` section 5 and asked on a decision card; the maintainer chose "Yes, exception for spec 001". So this plan, with its dependencies, counts as approved once a plan check finds no blocking issue. It does not cover the `HEAD` wording change to R2 and R7 of the approved spec (round 2 N10, round 3 N1), which goes to the maintainer as a separate Level 2 pull request. Review rounds may exceed the two-round limit while each round reduces the blocking findings.

## Deviations

Pull request 2 (Api error contract), fixes made without asking under `docs/workflow/implementation.md`:

- **`WithCulture="false"` on the embedded resource** (a blocker). The `.en` in `errors.en.json` made MSBuild treat the file as an `en` culture resource and move it into a satellite assembly, so the main assembly had no resource. The item in `Minioasis.Api.csproj` now says it has no culture; the resource name stays `Minioasis.Api.Resources.errors.en.json`.
- **`app.UseRouting()` after the error middleware in `Program.cs`** (a correctness piece of R3 and R5). Without it, `WebApplication` adds routing before the exception handler, and an exception thrown by routing itself (for example an ambiguous match) escapes the masked response. Tested by an ambiguous test route in `Server_failure_is_masked` and `Exception_logs_one_error_across_all_categories`.
- **One more file in `Minioasis.Api`:** `Shared/Models/ErrorMessage.cs`, the title and detail of one message, which the catalog, the provider, and the factory share (`AR-010`).
- **The mapper's shape.** `ApplicationResultHttpMapper.Map(result, onSuccess)` leaves a success response to the endpoint, because a success status is each capability's own decision; `MapError(error)` serves the status-code handler; the static `GetStatus(category)` is the one R1 table, which the writer also uses for an unhandled exception.
- **Three more test files in `Minioasis.Api.Tests`:** `Shared/TestDoubles/TestRequests.cs` (the named framework-error requests that two test classes share), `Shared/TestDoubles/ProblemResponseAssert.cs` (the exact R2 and R3 shape assertions that every Api test class shares), and `Shared/Hosting/ListenerFreeCollection.cs` (the xUnit definition of the non-parallel collection that the R4 logging-off test needs) (`TS-011`).
- **`HEAD`.** The spec change for `HEAD` (pull request #4) is not accepted yet, so the writer writes the body for `HEAD` as the approved spec says, with the decision in one place in `ErrorResponseWriter`. The `HEAD` tests assert the status and `Allow` only: `FrameworkErrorTests::Head_on_unknown_path_is_not_found` and `::Head_on_wrong_method_is_405_with_allow_header` replace the planned body assertions.
- **Test names.** `StartupTests::Inconsistent_messages_stop_the_host` is `Inconsistent_definitions_or_messages_stop_the_host`, because it also stops the host for an empty registry.
- **No dummy Koha settings in the test host yet.** Nothing reads them until pull request 3, which adds them with `AddKohaIntegration`; the host already applies per-host settings with `UseSetting`.

Pull request 3 (Koha configuration and generated client):

- **What `AddKohaIntegration` registers in this pull request.** The checked options and the two named clients, each with `HttpClient.Timeout` switched off. The rest of the file row arrives with the pull request whose tests reach it, so that no code is added before a test can fail for it (`TS-001`, `CD-007`): `TimeProvider.System` with the token provider, its first user (PR 4); header-log redaction in PR 5, whose R23 test of the 401 retry is the first that a logged header can fail (the token request of PR 4 carries no `Authorization` header, R15); the `SocketsHttpHandler { AllowAutoRedirect = false }` primary handlers with the redirect assertions (PR 5, as the breakdown says).
- **The exception contract on `KohaClientNames`** is written in PR 5, with the Koha exceptions and handlers it names. This pull request documents the two names only.
- **`KohaOptions.Deadline`** is the name of the timeout setting, because it is the deadline of one Koha interaction (D7, R21). `KohaOptions` is a class, not a record, so that no generated `ToString` can write the client secret (R13, `ER-008`); one more test file, `Minioasis.Koha.Tests/Shared/Options/KohaOptionsTests.cs`, guards it.
- **Test names.** The wiring tests of this pull request are `KohaServiceCollectionExtensionsTests::Registers_the_named_client` and five smaller tests; `::Registers_clients_and_handler_order`, with the handler-order and redirect assertions, is added in PR 5. `StartupTests::Invalid_koha_variable_stops_the_host` covers a blank base URL and a blank client id as well as the blank secret, and `::Starting_sends_nothing_to_koha` runs in `Development` and `Production`.
- **The focused contract's `info`** names its source instead of copying the official description. The operation, the `error` schema, the `oauth` tag, and the server are copied unchanged.

Pull request 4 (token provider):

- **More provider tests than the "Tests" table names**, because this pull request writes the code they reach (`TS-001`, `CD-007`): `KohaAccessTokenProviderTests::Refused_token_request_is_classified_by_status` and `::Network_error_is_unavailable` test which of the two Koha exceptions the provider throws at the token step, and with which `KohaStatus` (the pipeline test `KohaApiClientPipelineTests::Token_step_failures_by_row` stays in PR 5); `::Invalidate_discards_the_token_it_names` and `::Invalidate_keeps_a_newer_token` test `Invalidate(token)` directly, before the bearer handler uses it; `::Abandoned_request_failing_later_leaves_the_new_request_alone` covers an abandoned request that ends while a newer one is pending; `::Null_arguments_throw`. The "failure, then one more call" case of `::Ten_simultaneous_callers_share_one_request` is its own test, `::Ten_simultaneous_callers_share_one_failure_and_the_next_call_sends_a_new_request`. The wiring tests are `KohaServiceCollectionExtensionsTests::Registers_the_token_provider_on_the_oauth_client`, `::Registers_the_system_time_provider`, and `::Keeps_a_registered_time_provider`.
- **`KohaLoggingTests` holds the token step only.** `::No_entry_contains_secret_token_header_or_body` covers a successful token request and failing token replies with a marker in the body; `::Koha_exceptions_carry_no_body` covers the token-step exceptions. The protected call, the 401 retry, and a Koha error body on a call are added in PR 5 with the code that sends them.
- **One more test double,** `Minioasis.Koha.Tests/Shared/TestDoubles/StubHttpClientFactory.cs`: it records which named client a component asks for, and lets the provider tests pass options directly, so the base URL is tested without the trailing slash that `KohaOptionsFactory` adds (R15). `StubHttpMessageHandler` gains two replies: a body that breaks off with an `IOException` while it is read, and a reply that has already arrived in full, which is read even after the request was cancelled (R18). The stalling body and the deadline cases arrive with PR 5.
- **Token age on the monotonic clock.** The provider measures a token's age with `TimeProvider.GetTimestamp()` and `GetElapsedTime()`, not with `GetUtcNow()`, so a change of the system clock cannot make an expired token look fresh. The age counts from the moment the token request was sent. Tests drive it through `FakeTimeProvider`, as planned.
- **A token no longer than the margin is not reused** because its cache entry has no usable time left, not through a separate check. An explicit "not cached" check had no effect that any test, or any caller, could observe, so it was removed (`CD-007`); `verification.md` records how.
- **The Koha exceptions.** `KohaUnavailableException` keeps a network error (`HttpRequestException`; a bare `IOException` was dropped in PR 5 as unreachable) as its inner exception, because it holds no Koha body; it never holds the generated `KohaServiceException`. `KohaAuthenticationException` is always created with Koha's status, because Koha always replied; its `KohaStatus` property is nullable, as planned. Both exceptions and the provider are `public`, like the other types of `Minioasis.Koha`.
- **Observed for PR 5:** in .NET 10, `IHttpClientFactory`'s own logging already writes every header value as `*` (seen in a probe of the `OAuth` client's `Trace` entries). PR 5's explicit redaction is still planned; to see its R23 test fail, that pull request has to switch the default off on purpose.

Pull request 5 (handlers and failure mapping):

- **The 401 to the retry.** R19 discards "the token that got the 401" when Koha answers a `GET` with 401, and the retry is a `GET` too. So the bearer handler also discards the token that got the retry's 401, if it is still cached; the reply stays final (`EX-009`) and is never sent again. A token that the retry used successfully is kept. Tested in `KohaBearerHandlerTests::Get_with_401_then_401_is_final` and `::Get_with_401_then_200_is_retried_once_with_a_new_token`.
- **The retry request** is built again with the method, the address, and the headers, and the new token. The version and the request options are not copied: the generated clients set neither, and copying them had no effect that any test could observe (`CD-007`, `CD-008`).
- **The deadline handler's filter** is the `ER-009` form: any cancellation beneath the handler that is not the caller's is the deadline, because nothing else cancels there. A response whose body fails while it is read is disposed before the failure propagates. `KohaUnavailableException` gains a constructor without a status or an inner exception, for the deadline.
- **`KohaProtocolFailureMapper`** is a static class with two methods: `Map(int kohaStatus)` for the call step, and `Map(Exception exception)` for the documented exceptions of `KohaClientNames`. Any other exception, a cancellation included, is refused with `ArgumentException` rather than turned into an outcome, so that a caller's cancellation can never become a failure (`ER-009`, `CD-005`).
- **A body that breaks off arrives as `HttpRequestException`** around the `IOException`, because the deadline handler reads the body (`LoadIntoBufferAsync`) on both clients. The exception contract on `KohaClientNames` says so, and says that a bare `IOException` is not part of it. So the mapper has no `IOException` case and refuses one like any undocumented exception, and the token provider catches only `HttpRequestException` (review of pull request 9, B1, `CD-007`: as first written, both handled a bare `IOException` that no client as wired passes on). The provider's test of a body that breaks off sends the token request through the registered `OAuth` client, deadline handler included. The "Approach" list above and the R20 row of "Tests" still name `IOException` among the exceptions; changing that approved text is for the maintainer to accept.
- **Header redaction** is `RedactLoggedHeaders(_ => true)` on both clients: no header value is logged. .NET 10 already redacts every header value by default, so the explicit setting is proved by `KohaLoggingTests::Header_values_stay_redacted_when_the_application_default_logs_them`, which switches that default off for the whole application (`ConfigureHttpClientDefaults`). The setting survives such a default whether it is registered before or after `AddKohaIntegration` (both orders tried).
- **More tests than the "Tests" table names,** because this pull request writes the code they reach (`TS-001`, `CD-007`): `KohaDeadlineHandlerTests` (a reply in time, the deadline counted from the send, the caller's cancellation while waiting for headers and while reading the body, the coinciding case, network errors passed on, null arguments); `KohaApiClientPipelineTests::Protected_call_carries_the_token`, and `Deadline_covers_every_step` also covers the token step and the call together (6 of the 10 seconds spent getting the token) and the body. The shared-token and coinciding cases of `Caller_cancellation_propagates` are their own tests, `::Caller_cancelling_a_shared_token_wait_leaves_the_other_caller` and `::Caller_cancellation_wins_when_it_coincides_with_the_deadline`. `KohaLoggingTests` holds the protected call in its own theory, `::No_entry_of_a_protected_call_contains_secret_token_header_or_body`. `KohaProtocolFailureMapperTests::Refuses_an_undocumented_exception` and `::Null_exception_throws`. `KohaBearerHandlerTests` also covers `HEAD` among the other methods, and 403 next to 500 as a failure that is not retried.
- **`Maps_generated_exception_status`** reaches the generated `KohaServiceException` through the generated OAuth client, because no generated client for a protected call exists before spec 003; the later clients come from the same template (`nswag.json`).
- **One more test double,** `Minioasis.Koha.Tests/Shared/TestDoubles/KohaClientsHarness.cs`: the registration of `AddKohaIntegration` with a `FakeTimeProvider` and a fake Koha behind each named client, which the pipeline and the logging tests share (`TS-011`). `StubHttpMessageHandler` gains a body that stalls until the read is cancelled.
- **The pull request 4 review findings** in `KohaAccessTokenProvider`: N1, a repeatable test of the `Leave` guard (`KohaAccessTokenProviderTests::Last_waiter_leaving_after_its_request_ended_leaves_the_newer_request_alone`); N2, the catch takes only the caller's own cancellation; N3, a caller whose token is already cancelled stops before taking the gate and sends no token request (`::Already_cancelled_caller_sends_no_token_request`); N6, `IssuedToken` and `CachedToken` are plain classes (`::Nested_types_do_not_write_their_values_as_text`). The N1 test loops up to 2000 times and stops after 20 runs that reached the race, instead of about 50 runs: on this machine 50 runs reached none in four of six tries, and once warm 5 to 45 % of runs reach it.
