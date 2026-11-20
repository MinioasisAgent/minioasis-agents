- **Access:** Level 1
- **Last Updated:** 2026-11-20

# 001 — API errors and Koha connection: verification

- **Spec:** [spec.md](spec.md)
- **Verified at commit:** not yet
- **Transfer branch:** not yet

The spec is built in the five pull requests of `plan.md`, "Pull request breakdown". This record covers pull request 1, "Application error model", and pull request 2, "Api error contract". Every clause that a later pull request builds is listed below as "not yet built", with the pull request that builds it.

Test names are written `Project::Class::Method`, with `App` for `Minioasis.Application.Tests`, `Api` for `Minioasis.Api.Tests`, and the class's folder under `Shared/` (`Api::StartupTests` is at the project root). A number in brackets is the number of theory cases.

## Clause checklist

Clauses built in pull request 1:

| Spec clause | Cases tested | Proving test (`Project::Class::Method`) |
| --- | --- | --- |
| R1: the seven categories exist (validation, not found, method not allowed, unusable response, unavailable, authentication failed, unexpected) | Exactly these seven values, no more | `App::Errors.ApplicationErrorDefinitionTests::There_are_exactly_the_seven_categories_of_R1` |
| R1, R2, R3: which categories are client-visible (400, 404, 405) and which are masked (500, 502, 503) | Each of the seven categories | `App::Errors.ApplicationErrorDefinitionTests::Is_client_visible_only_for_validation_not_found_and_method_not_allowed` |
| R9 (registry part): given consistent definitions, the registry is created | Two distinct valid definitions; the real registry of seven | `App::Errors.ApplicationErrorDefinitionRegistryTests::Accepts_consistent_definitions`; `App::Extensions.ApplicationServiceCollectionExtensionsTests::Resolves_registry` |
| R9: a blank code fails, naming the problem | null, `""`, `" "`, `"\t\n"` | `App::Errors.ApplicationErrorDefinitionRegistryTests::Rejects_blank_code` |
| R9: a duplicate code fails, naming the code | The same code in the first and third places, with different categories | `App::Errors.ApplicationErrorDefinitionRegistryTests::Rejects_duplicate_code` |
| R9: a null definition fails, naming the problem | A null after a valid definition; a null field found by the scan reaches the registry | `App::Errors.ApplicationErrorDefinitionRegistryTests::Rejects_null_definition`; `App::Errors.ApplicationErrorDefinitionSourceTests::Scan_collects_every_public_static_definition_field_of_marked_classes_including_null_ones` |
| R9: an undefined category value fails, naming the code | 0 (unset), -1, 8 (one past the last), `int.MaxValue` | `App::Errors.ApplicationErrorDefinitionRegistryTests::Rejects_undefined_category` |
| R9: an empty registry fails, naming the problem | No definitions | `App::Errors.ApplicationErrorDefinitionRegistryTests::Rejects_empty_registry` |
| R9: every definition is checked (plan: the `[ErrorDefinitions]` scan) | Marked classes' public static definition fields, including null; unmarked classes ignored; the Application assembly yields the seven definitions | `App::Errors.ApplicationErrorDefinitionSourceTests::*` (4 tests) |
| R9: the registry checks the registered source, which a test host can replace | Replaced with a valid source; replaced with an empty source (fails on resolution) | `App::Extensions.ApplicationServiceCollectionExtensionsTests::Registry_checks_the_registered_source`; `::Inconsistent_registered_source_fails_when_the_registry_is_resolved` |
| R20: "authentication to a dependency failed" → `koha.authentication_failed` | `DependencyFailureOutcome.AuthenticationFailed` | `App::Errors.DependencyErrorDefinitionsTests::Selects_definition_and_category` |
| R20: "dependency unavailable" → `koha.unavailable` | `DependencyFailureOutcome.Unavailable` | same |
| R20: "dependency returned an unusable response" → `koha.invalid_response` | `DependencyFailureOutcome.InvalidResponse` | same; also `::Selection_returns_the_declared_definitions`, `::Every_outcome_has_a_definition`, `::Undefined_outcome_throws` (0, 4, -1) |
| R20, R5: the error carries Koha's status when there is one, apart from the placeholder values | Status set with and without values; status unset | `App::Errors.ApplicationErrorTests::Carries_definition_placeholder_values_and_dependency_status`; `::Dependency_status_is_not_a_placeholder_value`; `::Defaults_to_no_placeholder_values_and_no_dependency_status` |
| Spec "Errors": the internal codes `system.unexpected`, `koha.authentication_failed`, `koha.unavailable`, `koha.invalid_response`, and the public codes `not_found`, `method_not_allowed`, `request.invalid`, each with its category | All seven, as `plan.md`, "Error definitions", lists them | `App::Extensions.ApplicationServiceCollectionExtensionsTests::Resolves_registry` |

Clauses built in pull request 2:

| Spec clause | Cases tested | Proving test (`Project::Class::Method`) |
| --- | --- | --- |
| R1: each category's status, applied in one place | The seven categories, each through the mapper as an endpoint uses it (the body's code tells the mapper's answer from a framework 404); the table itself; the table covers exactly the seven categories; an undefined category throws (0, -1, 8) | `Api::Mappers.ApplicationResultHttpMapperTests::Maps_each_category_to_its_status` (7); `::Status_table_has_one_status_per_category` (7); `::Status_table_covers_exactly_the_seven_categories`; `::Undefined_category_throws` (3) |
| R2: content type `application/problem+json`; exactly `status` (number, equal to the HTTP status), `title`, `detail`, `code`, `traceId` (strings); no `type`, no `instance` | `not_found` from the framework and through the mapper, `method_not_allowed`, a capability's `test.not_found` (one placeholder), `test.two_placeholders` (two, one of them in both title and detail). Every R6 to R8a test uses the same exact-shape assertion | `Api::Writers.ErrorResponseTests::Client_visible_error_has_exact_members_and_content_type` (4) |
| R2: `title` and `detail` from `errors.en.json`, each `{name}` replaced by the supplied value | A message without placeholders, with one, with two; the dependency status is not a placeholder value; a value that looks like a placeholder is inserted as it is | `Api::Providers.ErrorMessageProviderTests::Fills_zero_one_and_two_placeholders` (3); `::Dependency_status_is_not_a_placeholder_value`; `::Values_are_inserted_as_they_are` |
| R2: a missing value or an extra value is a programming error | Missing: the only value, either of two, a name that differs in case. Extra: a value for a message without placeholders, an additional name, a name that differs in case. An error whose code has no message | `Api::Providers.ErrorMessageProviderTests::Missing_value_throws` (4); `::Extra_value_throws` (3); `::Error_without_a_message_throws` |
| R2 → R3, R5: the mismatch is answered as an unhandled exception | Missing value, one of two missing, wrong case, extra value, value for a message without placeholders: masked 500; one `Error` entry with the exception | `Api::Writers.ErrorResponseTests::Placeholder_mismatch_is_answered_as_masked_500` (5); `Api::Writers.ErrorLoggingTests::Exception_logs_one_error_across_all_categories` (the mismatch case) |
| R3: the masked body, exactly `status`, `title` "Internal error", `code` `internal_error`, `traceId`; no `detail` | `koha.authentication_failed` (500, with and without a dependency status), `koha.invalid_response` (502), `koha.unavailable` (503, with and without a dependency status), `system.unexpected` (500), an exception thrown by an endpoint (500), an exception thrown by routing (500); in `Development` and `Production` | `Api::Writers.ErrorResponseTests::Server_failure_is_masked` (14) |
| R4: `traceId` matches the W3C pattern, and the R5 or R5a entry carries the identical value | A client-visible error, an Application server failure, an unhandled exception | `Api::Hosting.TraceIdTests::Response_trace_id_is_w3c_and_matches_the_log` (3) |
| R4: two requests get different values | Two requests to one host | `Api::Hosting.TraceIdTests::Two_requests_get_different_ids` |
| R4: the activity exists without logging (plan: `ActivityListener`) | No logger provider, in `Development` and `Production`; the test first asserts that no other listener for `Microsoft.AspNetCore` is alive (non-parallel collection) | `Api::Hosting.TraceIdTests::Trace_id_present_when_logging_is_disabled` (2) |
| R5: an Application server failure writes exactly one `Error` entry, in any category, with the internal code and the `traceId`; R20's Koha status is in it | Each of the four server definitions; a dependency status set (401, 200, 503) and unset | `Api::Writers.ErrorLoggingTests::Server_failure_logs_one_error_with_code_and_trace_id` (7) |
| R5: an unhandled exception writes exactly one `Error` entry, in any category, with the exception and the `traceId`; ASP.NET's own entry is suppressed | An endpoint exception in `Development` and `Production`, a routing exception, a placeholder mismatch; the entry carries the thrown exception itself | `Api::Writers.ErrorLoggingTests::Exception_logs_one_error_across_all_categories` (4); `::Exception_entry_carries_the_thrown_exception` |
| R5, `ER-009`: an unhandled exception after the client disconnected (response not started) still writes exactly one `Error` entry, and the write does not throw | The writer called with a request whose abort token is already cancelled (unit test, no listening port) | `Api::Writers.ErrorResponseWriterTests::Unhandled_exception_after_the_client_disconnected_logs_one_error_and_does_not_throw` |
| R5a: exactly one `Information` entry (Minioasis categories) with the public code and the `traceId` | `not_found`, `method_not_allowed`, `request.invalid`, a capability's `test.not_found` | `Api::Writers.ErrorLoggingTests::Client_visible_error_logs_one_information_entry` (4) |
| R5a: no entry at `Warning` or above in any category, ASP.NET's included | Unknown path, `/`, wrong method on a one-method and a two-method route, malformed JSON, wrong type, empty body, no `Content-Type`, `text/plain`, over the size limit, non-numeric route value, missing query value, route constraint mismatch; each in `Development` and `Production`; also a capability's `test.not_found` in both | `Api::Writers.ErrorLoggingTests::Client_visible_error_logs_nothing_at_warning_or_above_in_any_category` (28) |
| R6: an unknown path is 404 `not_found` | An unknown path, an unknown path under a known prefix (`/test/`), `/`; `HEAD` on an unknown path (status, `Content-Type`, the one R5a entry; see "HEAD" below) | `Api::Handlers.FrameworkErrorTests::Unknown_path_is_not_found` (3); `::Head_on_unknown_path_is_not_found` |
| R7: a wrong method is 405 `method_not_allowed` with `Allow` listing exactly the accepted methods | One-method route (`DELETE`, `POST`, `OPTIONS`), two-method route (`DELETE`, `PUT`); `HEAD` on each (status, `Allow`, `Content-Type`, the one R5a entry) | `Api::Handlers.FrameworkErrorTests::Wrong_method_is_405_with_allow_header` (5); `::Head_on_wrong_method_is_405_with_allow_header` (2) |
| R8: an unreadable body is 400 `request.invalid` | Malformed JSON, a wrong type for a field, an empty body, no `Content-Type` (415 → 400), `text/plain` (415 → 400), over the size limit (413 → 400, through `OversizedBodyStartupFilter`, with a test timeout); each in `Development` and `Production` | `Api::Handlers.FrameworkErrorTests::Unreadable_body_is_request_invalid` (12) |
| R8: accepted media types | `application/json`, `application/json; charset=utf-8`, `Application/JSON`, `application/problem+json`, `application/vnd.minioasis.test+json; charset=utf-8` | `Api::Handlers.FrameworkErrorTests::Accepted_json_media_types_reach_the_endpoint` (5) |
| R8a: an unconvertible or missing parameter is 400 `request.invalid`; a failed route constraint is 404 | A non-numeric route value, a missing required query value, a non-numeric query value; a value that fails `{id:int}`; each in `Development` and `Production` | `Api::Handlers.FrameworkErrorTests::Unconvertible_or_missing_parameter_is_request_invalid` (6); `::Route_constraint_mismatch_is_not_found` (2) |
| Plan, `StatusCodeProblemHandler`: every other empty status is left unchanged | 401, 409, 429, 503 | `Api::Handlers.FrameworkErrorTests::Other_empty_statuses_are_left_unchanged` (4) |
| R9: the messages are checked against the registry, naming the code or the problem | No message for a client-visible definition; a message for a server failure; for an unknown code; a key that differs from a code only in case (rejected as unknown, ordinal comparison); a duplicate key; not valid JSON; a root that is not an object; a null message; a string or an array as the message; a missing title or detail; a blank title; an empty detail; a title that is not a string; a null detail. Consistent messages are accepted and looked up ordinally; the real file matches the real definitions | `Api::Factories.ErrorMessageCatalogFactoryTests::Startup_check_rejects` (16); `::Accepts_consistent_messages`; `::Real_messages_match_the_real_definitions` |
| R9: startup fails before any request is served; consistent definitions and messages start | The real registry and the embedded file start and serve the real `not_found` text; a missing message, an invalid file, and an empty registry each stop the host, naming the code or the problem | `Api::StartupTests::Real_registry_and_resource_file_are_consistent`; `::Inconsistent_definitions_or_messages_stop_the_host` (3) |
| R9: no definition escapes the scan (PR 1 review finding) | Every static field or property of type `ApplicationErrorDefinition`, of any visibility, in any type of the Application assembly is a public static field of a class marked `[ErrorDefinitions]` | `App::Errors.ApplicationErrorDefinitionSourceTests::Every_definition_in_the_application_assembly_is_a_public_static_field_of_a_marked_class` |
| R22 (Api part): a caller's abort is no failure and no `Error` entry | The caller cancels while the endpoint waits on the request's token | `Api::Extensions.ApiErrorHandlingTests::Aborted_request_logs_no_error` (a framework guard: ASP.NET handles the abort itself) |

**`HEAD`.** The spec on `master` writes the error body for every method, `HEAD` included, and the writer does so; the decision is one statement in `ErrorResponseWriter`. A change that removes the body for `HEAD` waits for the maintainer (pull request #4). Until then the `HEAD` tests assert the status, `Allow`, `Content-Type`, and the one R5a entry, never the body. Kestrel itself sends no body for a `HEAD` response; the test server does.

Not yet built: every other clause of R1 to R23, listed under "Requirement coverage". Their rows are added by the pull request that builds them.

## Requirement coverage

- **Proves:** what the test asserts, in plain English, so the maintainer can review behavior without reading C#.
- **Seen failing:** written before the code, or the behavior broken on purpose (say how) and restored.
- **Result:** ✅ pass, ❌ fail, ⚠️ gap (no test), or 🧑 needs maintainer (the tests cannot show it). The spec cannot be `verified` while any row is a fail or a gap. Every "needs maintainer" row appears in manual acceptance.

"Stubs" below means: the tests were written first, then run against production types that compiled but did nothing (`NotImplementedException`, empty lists, no registration). Each test failed for its own missing behavior before the real code was written.

For pull request 2, "stubs" were Api types that compiled and let the host start but did nothing: no endpoint module mapped, a writer and a status-code handler that wrote nothing, an empty message catalog, no startup check, no activity listener, `ThrowOnBadRequest` left at the framework default, a mapper that answered `Results.Empty`. Against them, 161 of the 164 Api tests then written failed. The three that passed: `Maps_each_category_to_its_status` for `not_found` passed for the wrong reason (no test route, so the framework's own 404); the test was tightened to assert the body's code with the capability's `test.not_found`, and all seven rows then failed against the stubs. `Status_table_covers_exactly_the_seven_categories` checks the test data and was broken on purpose (see below). `Head_on_unknown_path_is_not_found` holds without Minioasis code and was broken on purpose (see below). The five routing-exception cases were added after the code and were seen failing by removing the explicit `app.UseRouting()` from `Program.cs`.

**Broken on purpose (pull request 2).** Each change below was made to the finished code, the whole suite was run, and the named tests failed; the code was then restored.

| Change | Tests that failed |
| --- | --- |
| Activity listener not registered | `Trace_id_present_when_logging_is_disabled` (2) |
| Activity listener never disposed | `Trace_id_present_when_logging_is_disabled` (its precondition: another host's listener is alive) |
| `ThrowOnBadRequest` left at the default | `Unreadable_body_is_request_invalid`, `Unconvertible_or_missing_parameter_is_request_invalid`, `Client_visible_error_logs_nothing_at_warning_or_above_in_any_category` (the `Development` rows, 13) |
| `SuppressDiagnosticsCallback` removed | `Exception_logs_one_error_across_all_categories`, `Exception_entry_carries_the_thrown_exception` |
| The check between `Build()` and `Run()` removed | `Inconsistent_definitions_or_messages_stop_the_host` (3) |
| `app.UseRouting()` removed from `Program.cs` | `Server_failure_is_masked` and `Exception_logs_one_error_across_all_categories` (routing-exception rows) |
| Endpoint modules not mapped | 82 tests |
| Codes compared ignoring case in the catalog factory; in the catalog | `Startup_check_rejects` (the case row); `Accepts_consistent_messages` |
| 502 and 503 swapped; 405 mapped to 400; an undefined category mapped to 500 | `Maps_each_category_to_its_status`, `Status_table_has_one_status_per_category`, `Server_failure_is_masked`; the R7 tests; `Undefined_category_throws` |
| An eighth category value added | `Status_table_covers_exactly_the_seven_categories`, and the Application tests of PR 1 |
| The writer drops `Allow`; answers `HEAD` with 200 | `Wrong_method_is_405_with_allow_header`, `Head_on_wrong_method_is_405_with_allow_header`; `Head_on_unknown_path_is_not_found`, `Head_on_wrong_method_is_405_with_allow_header` |
| Placeholders filled in two passes; missing-value check removed; extra-value check removed | `Values_are_inserted_as_they_are`; `Missing_value_throws`; `Extra_value_throws`, `Placeholder_mismatch_is_answered_as_masked_500` |
| The handler shapes every status; 404, 413, or 415 not shaped | `Other_empty_statuses_are_left_unchanged`; the R6, R8, and R5a tests for that status |
| A `detail` in the masked body; a `type` in the client-visible body | 30 and 35 tests (every R2, R3, R6 to R8a test) |
| Client error logged at `Warning`; without its `traceId`; server failure without its dependency status; exception entry without the exception | `Client_visible_error_logs_*`, `Response_trace_id_is_w3c_and_matches_the_log`; the same; `Server_failure_logs_one_error_with_code_and_trace_id`; `Exception_*` |
| `traceId` from `HttpContext.TraceIdentifier` instead of the activity | 68 tests |
| Each check of the catalog factory removed in turn (duplicate, unknown code, server failure, missing message, null, blank, non-string, root not an object, invalid JSON) | `Startup_check_rejects` each time; also `Inconsistent_definitions_or_messages_stop_the_host` for missing message and invalid JSON |
| The mapper answers a success with 204; its null guard removed | `Success_is_answered_by_the_endpoint`; `Null_arguments_throw` |
| The writer writes with the request's abort token again (review B1) | `Unhandled_exception_after_the_client_disconnected_logs_one_error_and_does_not_throw` (the write throws `TaskCanceledException`); written first, it failed the same way against the code before the fix |
| No `Content-Type` for `HEAD`; no `Information` entry for `HEAD` | `Head_on_unknown_path_is_not_found`, `Head_on_wrong_method_is_405_with_allow_header` (2), each time |
| A capability's client-visible error logged at `Warning` | `Client_visible_error_logs_nothing_at_warning_or_above_in_any_category` (the two capability rows), `Client_visible_error_logs_one_information_entry` |
| A temporary `Error` log in the abort path (a middleware inside the exception handler that logs any `OperationCanceledException`) | `Aborted_request_logs_no_error` |
| PR 1 findings: an internal static definition field, a public static definition property, or an unmarked class in the Application assembly; each null guard removed | `Every_definition_in_the_application_assembly_is_a_public_static_field_of_a_marked_class`; `Null_source_throws`, `Null_definitions_throw`, `Null_assembly_throws`. Removing the guard of `AddApplication()` alone does not fail `Null_services_throw`, because `AddSingleton` then throws the same `ArgumentNullException` for `services`; replacing the guard with an early return made it fail |

| Req | Proves (Given / When / Then) | Test (`Project::Class::Method`) | Seen failing | Result |
| --- | --- | --- | --- | --- |
| R1 (categories) | There are exactly seven categories. Validation, not found, and method not allowed are client-visible; the other four are masked. | `App::Errors.ApplicationErrorDefinitionTests::There_are_exactly_the_seven_categories_of_R1`; `::Is_client_visible_only_for_validation_not_found_and_method_not_allowed` | Visibility: against stubs. Seven categories: broken on purpose by adding an eighth category value; the test failed; restored | ✅ |
| R1 (status per category) | Given an Application failure of each of the seven categories, when Api maps it, then the status is 400, 404, 405, 502, 503, 500, 500; the table is one method, and an undefined category throws. | `Api::Mappers.ApplicationResultHttpMapperTests::*` (4 tests, 18 cases) | Against stubs (after tightening, see above); broken on purpose by swapping 502 and 503, by mapping 405 to 400, and by adding an eighth category | ✅ |
| R2 | Given a client-visible error, when it is returned, then the content type is `application/problem+json` and the body has exactly `status`, `title`, `detail`, `code`, `traceId`, with the message from `errors.en.json` and its placeholders filled. A missing or extra value is answered as a masked 500 with one `Error` entry. | `Api::Writers.ErrorResponseTests::Client_visible_error_has_exact_members_and_content_type`, `::Placeholder_mismatch_is_answered_as_masked_500`; `Api::Providers.ErrorMessageProviderTests::*` (6 tests, 13 cases) | Against stubs; broken on purpose (two-pass filling, each mismatch check removed, a `type` member added) | ✅ |
| R3 | Given a server failure (each server category, `unexpected`, an exception thrown by an endpoint or by routing), when it is returned, in `Development` or `Production`, then the body is exactly `status`, `title` "Internal error", `code` `internal_error`, `traceId`, with no `detail`. | `Api::Writers.ErrorResponseTests::Server_failure_is_masked` (14) | Against stubs; routing rows by removing `app.UseRouting()`; broken on purpose by adding a `detail` | ✅ |
| R4 | Given any error response, when it is written, then `traceId` is W3C, the R5 or R5a entry carries the same value, two requests differ, and the identifier exists with logging switched off. | `Api::Hosting.TraceIdTests::*` (3 tests, 6 cases) | Against stubs; broken on purpose (listener not registered, not disposed; identifier from `TraceIdentifier`) | ✅ |
| R5 | Given a server failure, when it is returned, then exactly one `Error` entry exists across all categories: with the internal code, the `traceId`, and Koha's status when set, for an Application failure; with the exception and the `traceId` for an unhandled exception, ASP.NET's own entry suppressed, also when the client has already disconnected. The Koha clause (no `Error` entry from the Koha connection) is built in PR 5. | `Api::Writers.ErrorLoggingTests::Server_failure_logs_one_error_with_code_and_trace_id` (7), `::Exception_logs_one_error_across_all_categories` (4), `::Exception_entry_carries_the_thrown_exception`; `Api::Writers.ErrorResponseWriterTests::Unhandled_exception_after_the_client_disconnected_logs_one_error_and_does_not_throw` | Against stubs; broken on purpose (suppression removed, dependency status or exception dropped, the abort token restored on the write) | ✅ |
| R5a | Given a client-visible error, when it is returned, then Minioasis writes exactly one entry, at `Information`, with the code and the `traceId`, and no category writes anything at `Warning` or above. | `Api::Writers.ErrorLoggingTests::Client_visible_error_logs_one_information_entry` (4), `::Client_visible_error_logs_nothing_at_warning_or_above_in_any_category` (28) | Against stubs; broken on purpose (logged at `Warning`; without the `traceId`) | ✅ |
| R6 | Given a path that matches no route (unknown, under a known prefix, `/`), when it is answered, then 404 with the R2 body and `not_found`; `HEAD` gets 404. | `Api::Handlers.FrameworkErrorTests::Unknown_path_is_not_found` (3), `::Head_on_unknown_path_is_not_found` | Against stubs; the `HEAD` test holds without Minioasis code and was broken on purpose (the writer answering `HEAD` with 200) | ✅ |
| R7 | Given a route with one or two methods, when another method (including `OPTIONS` and `HEAD`) is used, then 405 with `method_not_allowed` and `Allow` listing exactly the accepted methods. | `Api::Handlers.FrameworkErrorTests::Wrong_method_is_405_with_allow_header` (5), `::Head_on_wrong_method_is_405_with_allow_header` (2) | Against stubs; broken on purpose (the writer dropping `Allow`) | ✅ |
| R8 | Given an endpoint that reads JSON, when the body is malformed, of the wrong type, empty, without `Content-Type`, `text/plain`, or over the size limit, then 400 `request.invalid` in both environments (never 413, 415, or 500); the accepted media types reach the endpoint. | `Api::Handlers.FrameworkErrorTests::Unreadable_body_is_request_invalid` (12), `::Accepted_json_media_types_reach_the_endpoint` (5) | Against stubs; broken on purpose (`ThrowOnBadRequest` at the default; 413 or 415 not shaped) | ✅ |
| R8a | Given a matched route, when a route or query value cannot be converted or a required query value is missing, then 400 `request.invalid`; a failed route constraint is 404 `not_found`; both environments. | `Api::Handlers.FrameworkErrorTests::Unconvertible_or_missing_parameter_is_request_invalid` (6), `::Route_constraint_mismatch_is_not_found` (2) | Against stubs; broken on purpose (`ThrowOnBadRequest` at the default) | ✅ |
| R9 (registry) | Given definitions with a blank code, a duplicate code, a null definition, an undefined category, or none at all, when the registry is created, then it throws, naming the code where there is one and otherwise the problem. Given consistent definitions, it is created. Every `[ErrorDefinitions]` field, including a null one, reaches the check, and the registry checks the source that is registered. | `App::Errors.ApplicationErrorDefinitionRegistryTests::*` (6 tests, 12 cases); `App::Errors.ApplicationErrorDefinitionSourceTests::*` (4); `App::Extensions.ApplicationServiceCollectionExtensionsTests::Registry_checks_the_registered_source`, `::Inconsistent_registered_source_fails_when_the_registry_is_resolved` | Against stubs. `Inconsistent_registered_source_fails_when_the_registry_is_resolved` first passed for the wrong reason (no service registered); it was tightened to require the registry's own "empty" message, then failed against stubs. `Scan_ignores_unmarked_classes`: broken on purpose by removing the attribute filter from the scan; the test failed; restored | ✅ |
| R9 (messages and startup) | Given messages that disagree with the definitions in any way `ER-011` lists, when the catalog is built, then it throws naming the code or the problem; keys are compared ordinally. Given an inconsistency, the host stops before serving a request; given consistent definitions and messages, it starts. | `Api::Factories.ErrorMessageCatalogFactoryTests::*` (3 tests, 18 cases); `Api::StartupTests::*` (2 tests, 4 cases) | Against stubs; broken on purpose (each check removed in turn; case-insensitive comparison; the startup check removed) | ✅ |
| R10 | Missing or blank variable | — | not yet built (PR 3) | ⚠️ |
| R11 | Base URL rules | — | not yet built (PR 3) | ⚠️ |
| R12 | Timeout rules | — | not yet built (PR 3) | ⚠️ |
| R13 | No value in a startup message | — | not yet built (PR 3) | ⚠️ |
| R14 | No request to Koha at startup | — | not yet built (PR 3) | ⚠️ |
| R15 | The token request | — | not yet built (PR 4) | ⚠️ |
| R16 | Token reply validation | — | not yet built (PR 4) | ⚠️ |
| R17 | Token cache and refresh margin | — | not yet built (PR 4) | ⚠️ |
| R18 | One shared token request | — | not yet built (PR 4; the shared deadline in PR 5) | ⚠️ |
| R19 | Bearer header and the 401 retry | — | not yet built (PR 5) | ⚠️ |
| R20 (selection) | Given each shared Koha failure outcome, when the definition is selected, then it is `koha.authentication_failed` (authentication failed), `koha.unavailable` (unavailable), or `koha.invalid_response` (unusable response), each with its category; an undefined outcome throws. The error carries Koha's status apart from the placeholder values. | `App::Errors.DependencyErrorDefinitionsTests::*` (4 tests, 8 cases); `App::Errors.ApplicationErrorTests::Carries_definition_placeholder_values_and_dependency_status`, `::Dependency_status_is_not_a_placeholder_value` | Against stubs | ✅ |
| R20 (translation) | Koha statuses and exceptions to outcomes, per step | — | not yet built (PR 5) | ⚠️ |
| R21 | One deadline for the whole call | — | not yet built (PR 5) | ⚠️ |
| R22 (Api part) | Given a request whose caller aborts, when the endpoint observes it, then no `Error` entry is written and Minioasis logs nothing. A framework guard: ASP.NET handles the abort without calling the exception handler. | `Api::Extensions.ApiErrorHandlingTests::Aborted_request_logs_no_error` | Broken on purpose by adding a temporary `Error` log in the abort path (a middleware inside the exception handler that logs any `OperationCanceledException`) | ✅ |
| R22 (Koha part) | Caller cancellation through a protected call | — | not yet built (PR 5) | ⚠️ |
| R23 | No secret, token, header, or body in any log | — | not yet built (PR 4 for the token part, PR 5) | ⚠️ |

Supporting tests of pull request 2, not tied to one clause: `Api::Mappers.ApplicationResultHttpMapperTests::Success_is_answered_by_the_endpoint`, `::Null_arguments_throw`; `App::Errors.ApplicationErrorDefinitionRegistryTests::Null_source_throws`; `App::Errors.ApplicationErrorDefinitionSourceTests::Null_definitions_throw`, `::Null_assembly_throws`; `App::Extensions.ApplicationServiceCollectionExtensionsTests::Null_services_throw`.

Supporting tests of pull request 1, not tied to one clause: `App::Errors.ApplicationErrorDefinitionTests::Carries_code_and_category` (broken on purpose by altering the stored code; it failed with nine other tests; restored); `App::Errors.ApplicationErrorTests::Placeholder_values_are_copied`, `::Null_definition_throws`, `::Null_placeholder_value_throws`; `App::Results.ApplicationResultTests::*` (4 tests). All seen failing against stubs.

## Edge cases and prohibitions

| Item from the spec | State | Proven by |
| --- | --- | --- |
| A message placeholder has no supplied value, or a value is supplied for no placeholder | covered | R2: `Api::Providers.ErrorMessageProviderTests::Missing_value_throws`, `::Extra_value_throws`; `Api::Writers.ErrorResponseTests::Placeholder_mismatch_is_answered_as_masked_500` |
| The request carries a valid `traceparent` header | dismissed | Whether the incoming trace is continued does not change the contract: R4 still holds for the response and the log |
| An exception is thrown after the response has started | dismissed | The status and headers are already sent and cannot be replaced; the exception is still logged at least once with the request's trace identifier; R5 does not apply |
| `HEAD` or `OPTIONS` on a `GET` route | covered | R7: `Api::Handlers.FrameworkErrorTests::Wrong_method_is_405_with_allow_header` (`OPTIONS`), `::Head_on_wrong_method_is_405_with_allow_header` (status and `Allow`; see "HEAD" above) |
| Kestrel rejects a request before routing (header too large, URI too long) | dismissed | Kestrel answers before any Minioasis code runs, so no middleware can shape the reply |
| `MINIOASIS_KOHA_TIMEOUT_SECONDS` has a sign, leading zeros, spaces, non-ASCII digits, or overflows | covered | R12: not yet built (PR 3) |
| Base URL with a query string, fragment, or user information | covered | R11: not yet built (PR 3) |
| Koha's token endpoint returns 400 or 403 (wrong client id or secret) | covered | R20, token step: not yet built (PR 5) |
| The token expires between the cache check and Koha's receipt of the request | covered | R19: not yet built (PR 5) |
| Simultaneous token refresh after a 401 retry by several callers | covered | R18, R19: not yet built (PR 5) |
| The real Koha's token reply differs from the contract | manual | D10: checked in the first Koha capability's manual acceptance |
| A 2xx reply with an empty body where the contract has one | covered | R20, last row: not yet built (PR 5) |
| A Koha response body that arrives slowly | covered | R21: not yet built (PR 5) |
| A large Koha response body that arrives quickly | dismissed | Koha is the trusted authority, and a size limit, if one is needed, depends on the operation and belongs to the capability's spec |
| Koha cannot be reached on the first call after startup | covered | R20, token step, row 1: not yet built (PR 5) |
| A 413 body too large, or a route or query value that cannot be converted | covered | R8, R8a: `Api::Handlers.FrameworkErrorTests::Unreadable_body_is_request_invalid` (over the size limit), `::Unconvertible_or_missing_parameter_is_request_invalid` |
| Must not reveal a provider name, internal code, exception, or diagnosis in a 500, 502, or 503 response | covered | R3: `Api::Writers.ErrorResponseTests::Server_failure_is_masked` (exact members and values). The masked categories are fixed by `App::Errors.ApplicationErrorDefinitionTests::Is_client_visible_only_for_validation_not_found_and_method_not_allowed` |
| Must not return an error body in any shape other than Problem Details | covered | R2, R3, R6 to R8a: every test in `Api::Writers.ErrorResponseTests` and `Api::Handlers.FrameworkErrorTests` asserts the exact shape |
| Must not log an access token, the client secret, an `Authorization` header, or a Koha response body | covered | R23: not yet built (PR 4, PR 5); also manual scenario M3 |
| Must not write a configuration value into a startup error | covered | R13: not yet built (PR 3) |
| Must not send a non-`GET` request to Koha more than once | covered | R19: not yet built (PR 5) |
| Must not retry a `GET` more than once | covered | R19: not yet built (PR 5) |
| Must not request a token for every call while a valid token is cached | covered | R17: not yet built (PR 4) |
| Must not send the token request through the client that attaches bearer tokens | covered | R15: not yet built (PR 4) |
| Must not treat a caller's cancellation as a failure, or log it as an error | covered | R22: Api part `Api::Extensions.ApiErrorHandlingTests::Aborted_request_logs_no_error`; Koha part not yet built (PR 5) |
| Must not contact Koha during startup | covered | R14: not yet built (PR 3) |
| Must not start with an inconsistent error registry or invalid configuration | covered | R9 to R12. Registry: `App::Errors.ApplicationErrorDefinitionRegistryTests::*`. Messages and startup: `Api::Factories.ErrorMessageCatalogFactoryTests::Startup_check_rejects`, `Api::StartupTests::Inconsistent_definitions_or_messages_stop_the_host`. Configuration: not yet built (PR 3) |
| Must not call a real Koha from a test | dismissed | A process rule, not behavior: `CLAUDE.md`, Level 3, forbids it, and review enforces it |

## Wiring

| Piece | Registered in | Reached from | Proven by |
| --- | --- | --- | --- |
| `ApplicationErrorDefinitionSource` (default: the `[ErrorDefinitions]` scan of the Application assembly) | `AddApplication()`, `Minioasis.Application/Shared/Extensions/ApplicationServiceCollectionExtensions.cs` (singleton), called by `Program.cs` | The registry | `App::Extensions.ApplicationServiceCollectionExtensionsTests::Resolves_registry`; `App::Errors.ApplicationErrorDefinitionSourceTests::Scan_of_the_application_assembly_finds_the_seven_definitions` |
| `ApplicationErrorDefinitionRegistry` | `AddApplication()` (singleton, checked on first resolution) | The message catalog, resolved in `Program.cs` between `Build()` and `Run()` | `App::Extensions.ApplicationServiceCollectionExtensionsTests::Resolves_registry`, `::Registry_is_a_single_instance`, `::Registry_checks_the_registered_source`, `::Inconsistent_registered_source_fails_when_the_registry_is_resolved` |
| `ErrorMessageSource` (default: the embedded `Resources/errors.en.json`) | `AddApiErrorHandling()`, `Minioasis.Api/Shared/Extensions/ApiErrorHandlingExtensions.cs` (singleton) | The message catalog | `Api::StartupTests::Real_registry_and_resource_file_are_consistent`; `Api::Factories.ErrorMessageCatalogFactoryTests::Real_messages_match_the_real_definitions` |
| `ErrorMessageCatalog`, built and checked by `ErrorMessageCatalogFactory` | `AddApiErrorHandling()` (singleton) | `Program.cs`, between `Build()` and `Run()` (the R9 startup check); `ErrorMessageProvider` | `Api::StartupTests::*` |
| `ErrorMessageProvider`, `ErrorResponseWriter`, `ApplicationResultHttpMapper`, `StatusCodeProblemHandler` | `AddApiErrorHandling()` (singletons) | The exception handler and status-code pages resolve the writer and the handler per request; endpoints receive the mapper | Every test in `Api::Writers`, `Api::Handlers`, `Api::Mappers` runs on the real host |
| Exception handler (delegate, `SuppressDiagnosticsCallback`) and status-code pages | `UseApiErrorHandling()`, called by `Program.cs` before `UseRouting()` | Every request | `Api::Writers.ErrorResponseTests::Server_failure_is_masked`; `Api::Handlers.FrameworkErrorTests::*` |
| `RouteHandlerOptions.ThrowOnBadRequest = false` | `AddApiErrorHandling()` | Every endpoint, in every environment | The `Development` rows of `Api::Handlers.FrameworkErrorTests::Unreadable_body_is_request_invalid` |
| `RequestActivityListenerHostedService` | `AddRequestActivityListener()`, called by `AddApiErrorHandling()` (hosted service) | Started and stopped with the host | `Api::Hosting.TraceIdTests::Trace_id_present_when_logging_is_disabled` |
| `IEndpointModule` registrations | None in production yet; the test host registers `TestEndpointModule` | `MapEndpointModules()`, called by `Program.cs` | Every test that reaches a `/test/` route |
| `DependencyErrorDefinitions.ForKohaOutcome` | Not registered (static selection) | Not yet: the first capability's use case (spec 003), as `plan.md`, "Risks", states | `App::Errors.DependencyErrorDefinitionsTests::*` |

## Checks

| Commit | Date | `scripts/preflight.sh` | Tests passed / total |
| --- | --- | --- | --- |
| `beb848d` | 2026-11-20 | passed | 47 / 47 (`Minioasis.Application.Tests`; the Api and Koha test projects hold no tests yet) |
| `a0d557e` | 2026-11-20 | passed | 219 / 219 (52 `Minioasis.Application.Tests`, 167 `Minioasis.Api.Tests`; the Koha test project holds no tests yet) |
| review fixes (B1, N1, N2) | 2026-11-20 | passed | 222 / 222 (52 `Minioasis.Application.Tests`, 170 `Minioasis.Api.Tests`) |

## Independent verification

- **Verdict:** not yet. The spec is verified after its fifth pull request.
- **Blocking findings:** none yet
- **Non-blocking findings:** none yet

## Manual acceptance (maintainer)

The scenarios are the spec's M1 to M3 (D10). The maintainer runs them once the spec is built.

- **Koha instance and version:**
- **Date:**

| # | Scenario | Expected | Observed (maintainer's words) | Result |
| --- | --- | --- | --- | --- |
| M1 | Start Minioasis with `MINIOASIS_KOHA_CLIENT_SECRET` unset; with `MINIOASIS_KOHA_BASE_URL` set to an `http://` address in `Production`; with `MINIOASIS_KOHA_TIMEOUT_SECONDS=0`. Then start it with valid values | Each of the three refuses to start with a message that names the variable and does not show its value; with valid values it starts | | |
| M2 | With Minioasis running, send `GET /does-not-exist` | 404, content type `application/problem+json`, body with `status`, `title` "Not found", `detail` "No resource exists at this address.", `code` `not_found`, and a `traceId` | | |
| M3 | Read the log of M1 and M2 | The `traceId` values from M2 appear in the log; the client secret appears nowhere | | |

An "issue" becomes a fix in this repository, followed by a new `ready/` branch.
