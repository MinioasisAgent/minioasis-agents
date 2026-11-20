- **Access:** Level 1
- **Last Updated:** 2026-11-20

# 001 — API errors and Koha connection: verification

- **Spec:** [spec.md](spec.md)
- **Verified at commit:** not yet
- **Transfer branch:** not yet

The spec is built in the five pull requests of `plan.md`, "Pull request breakdown". This record covers pull request 1, "Application error model". Every clause that a later pull request builds is listed below as "not yet built", with the pull request that builds it.

Test names are written `Project::Class::Method`, with `App` for `Minioasis.Application.Tests` and the class's folder under `Shared/`.

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

Not yet built: every other clause of R1 to R23, listed under "Requirement coverage". Their rows are added by the pull request that builds them.

## Requirement coverage

- **Proves:** what the test asserts, in plain English, so the maintainer can review behavior without reading C#.
- **Seen failing:** written before the code, or the behavior broken on purpose (say how) and restored.
- **Result:** ✅ pass, ❌ fail, ⚠️ gap (no test), or 🧑 needs maintainer (the tests cannot show it). The spec cannot be `verified` while any row is a fail or a gap. Every "needs maintainer" row appears in manual acceptance.

"Stubs" below means: the tests were written first, then run against production types that compiled but did nothing (`NotImplementedException`, empty lists, no registration). Each test failed for its own missing behavior before the real code was written.

| Req | Proves (Given / When / Then) | Test (`Project::Class::Method`) | Seen failing | Result |
| --- | --- | --- | --- | --- |
| R1 (categories) | There are exactly seven categories. Validation, not found, and method not allowed are client-visible; the other four are masked. | `App::Errors.ApplicationErrorDefinitionTests::There_are_exactly_the_seven_categories_of_R1`; `::Is_client_visible_only_for_validation_not_found_and_method_not_allowed` | Visibility: against stubs. Seven categories: broken on purpose by adding an eighth category value; the test failed; restored | ✅ |
| R1 (status per category) | The mapper's status for each category | — | not yet built (PR 2) | ⚠️ |
| R2 | Client-visible body, members, messages, placeholders | — | not yet built (PR 2) | ⚠️ |
| R3 | Masked server failures | — | not yet built (PR 2) | ⚠️ |
| R4 | `traceId` format and match with the log | — | not yet built (PR 2) | ⚠️ |
| R5 | One `Error` entry per server failure | — | not yet built (PR 2); the error's `DependencyStatus`, which R5 logs, exists (see R20) | ⚠️ |
| R5a | One `Information` entry per client-visible error | — | not yet built (PR 2) | ⚠️ |
| R6 | Unknown path is `not_found` | — | not yet built (PR 2) | ⚠️ |
| R7 | Wrong method is 405 with `Allow` | — | not yet built (PR 2) | ⚠️ |
| R8 | Unreadable body is `request.invalid` | — | not yet built (PR 2) | ⚠️ |
| R8a | Unconvertible or missing parameter is `request.invalid` | — | not yet built (PR 2) | ⚠️ |
| R9 (registry) | Given definitions with a blank code, a duplicate code, a null definition, an undefined category, or none at all, when the registry is created, then it throws, naming the code where there is one and otherwise the problem. Given consistent definitions, it is created. Every `[ErrorDefinitions]` field, including a null one, reaches the check, and the registry checks the source that is registered. | `App::Errors.ApplicationErrorDefinitionRegistryTests::*` (6 tests, 12 cases); `App::Errors.ApplicationErrorDefinitionSourceTests::*` (4); `App::Extensions.ApplicationServiceCollectionExtensionsTests::Registry_checks_the_registered_source`, `::Inconsistent_registered_source_fails_when_the_registry_is_resolved` | Against stubs. `Inconsistent_registered_source_fails_when_the_registry_is_resolved` first passed for the wrong reason (no service registered); it was tightened to require the registry's own "empty" message, then failed against stubs. `Scan_ignores_unmarked_classes`: broken on purpose by removing the attribute filter from the scan; the test failed; restored | ✅ |
| R9 (messages and startup) | The checks against `errors.en.json`, and startup stopping before the first request | — | not yet built (PR 2) | ⚠️ |
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
| R22 | Caller cancellation propagates | — | not yet built (PR 2 for the Api part, PR 5 for Koha) | ⚠️ |
| R23 | No secret, token, header, or body in any log | — | not yet built (PR 4 for the token part, PR 5) | ⚠️ |

Supporting tests of pull request 1, not tied to one clause: `App::Errors.ApplicationErrorDefinitionTests::Carries_code_and_category` (broken on purpose by altering the stored code; it failed with nine other tests; restored); `App::Errors.ApplicationErrorTests::Placeholder_values_are_copied`, `::Null_definition_throws`, `::Null_placeholder_value_throws`; `App::Results.ApplicationResultTests::*` (4 tests). All seen failing against stubs.

## Edge cases and prohibitions

| Item from the spec | State | Proven by |
| --- | --- | --- |
| A message placeholder has no supplied value, or a value is supplied for no placeholder | covered | R2: not yet built (PR 2) |
| The request carries a valid `traceparent` header | dismissed | Whether the incoming trace is continued does not change the contract: R4 still holds for the response and the log |
| An exception is thrown after the response has started | dismissed | The status and headers are already sent and cannot be replaced; the exception is still logged at least once with the request's trace identifier; R5 does not apply |
| `HEAD` or `OPTIONS` on a `GET` route | covered | R7: not yet built (PR 2) |
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
| A 413 body too large, or a route or query value that cannot be converted | covered | R8, R8a: not yet built (PR 2) |
| Must not reveal a provider name, internal code, exception, or diagnosis in a 500, 502, or 503 response | covered | R3: not yet built (PR 2). The masked categories are fixed by `App::Errors.ApplicationErrorDefinitionTests::Is_client_visible_only_for_validation_not_found_and_method_not_allowed` |
| Must not return an error body in any shape other than Problem Details | covered | R2, R3, R6 to R8: not yet built (PR 2) |
| Must not log an access token, the client secret, an `Authorization` header, or a Koha response body | covered | R23: not yet built (PR 4, PR 5); also manual scenario M3 |
| Must not write a configuration value into a startup error | covered | R13: not yet built (PR 3) |
| Must not send a non-`GET` request to Koha more than once | covered | R19: not yet built (PR 5) |
| Must not retry a `GET` more than once | covered | R19: not yet built (PR 5) |
| Must not request a token for every call while a valid token is cached | covered | R17: not yet built (PR 4) |
| Must not send the token request through the client that attaches bearer tokens | covered | R15: not yet built (PR 4) |
| Must not treat a caller's cancellation as a failure, or log it as an error | covered | R22: not yet built (PR 2, PR 5) |
| Must not contact Koha during startup | covered | R14: not yet built (PR 3) |
| Must not start with an inconsistent error registry or invalid configuration | covered | R9 to R12. Registry part: `App::Errors.ApplicationErrorDefinitionRegistryTests::*`. Startup, messages, and configuration: not yet built (PR 2, PR 3) |
| Must not call a real Koha from a test | dismissed | A process rule, not behavior: `CLAUDE.md`, Level 3, forbids it, and review enforces it |

## Wiring

| Piece | Registered in | Reached from | Proven by |
| --- | --- | --- | --- |
| `ApplicationErrorDefinitionSource` (default: the `[ErrorDefinitions]` scan of the Application assembly) | `AddApplication()`, `Minioasis.Application/Shared/Extensions/ApplicationServiceCollectionExtensions.cs` (singleton) | The registry. `Program.cs` does not call `AddApplication()` yet (PR 2) | `App::Extensions.ApplicationServiceCollectionExtensionsTests::Resolves_registry`; `App::Errors.ApplicationErrorDefinitionSourceTests::Scan_of_the_application_assembly_finds_the_seven_definitions` |
| `ApplicationErrorDefinitionRegistry` | `AddApplication()` (singleton, checked on first resolution) | Not yet: the message-catalog check resolves it at startup (PR 2) | `App::Extensions.ApplicationServiceCollectionExtensionsTests::Resolves_registry`, `::Registry_is_a_single_instance`, `::Registry_checks_the_registered_source`, `::Inconsistent_registered_source_fails_when_the_registry_is_resolved` |
| `DependencyErrorDefinitions.ForKohaOutcome` | Not registered (static selection) | Not yet: the first capability's use case (spec 003), as `plan.md`, "Risks", states | `App::Errors.DependencyErrorDefinitionsTests::*` |

## Checks

| Commit | Date | `scripts/preflight.sh` | Tests passed / total |
| --- | --- | --- | --- |
| `beb848d` | 2026-11-20 | passed | 47 / 47 (`Minioasis.Application.Tests`; the Api and Koha test projects hold no tests yet) |

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
