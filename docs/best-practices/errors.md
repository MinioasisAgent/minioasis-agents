- **Access:** Level 2
- **Last Updated:** 2026-11-20

# Errors

Rules `ER-001` to `ER-011`. The format, and the meaning of the made-up "renew a loan" examples, are explained in [`README.md`](README.md).

These rules describe one error contract for the whole API. Every capability follows it, so that clients handle every endpoint's failures the same way. Changing any part of the contract that a client can see is a Level 2 change (see `CLAUDE.md`).

<a id="ER-001"></a>

## ER-001 Let Application define errors and Api present them

Application owns the stable, typed definition of each error and the meaning of each use-case outcome. Api owns the HTTP status and the text shown to the client. Keeping the two apart lets the wording and the transport change without touching business rules, and keeps HTTP out of Application (`AR-003`).

```csharp
// BAD: Application chooses a status code and English text
return new RenewLoanFailure(404, "Loan not found");

// GOOD: Application returns a typed error; Api turns it into a status and a message
return CreateFailure<RenewLoanResponse>(LoanErrorDefinitions.NotFound, metadata);
```

**Checked by:** review

---

<a id="ER-002"></a>

## ER-002 Define each error once, in its feature's error-definition group

An error has exactly one definition: a stable code and a category, declared in the error-definition group of its feature, in Application. A registry collects every definition, so that the application can check them all when it starts (`ER-011`). Nothing defines an error at the place where it is used.

To add an error:

1. Declare the definition, with its code and its category, in the feature's `<Feature>ErrorDefinitions` group.
2. If the client can act on it (validation or not found), add its message to the English resource file, under exactly the same code (`ER-011`).
3. If it is a server failure, add no message.
4. Return the error by referring to the definition, and supply a value for every placeholder in its message.

```csharp
// GOOD: declared once
public static class LoanErrorDefinitions
{
    public static readonly ApplicationErrorDefinition NotFound =
        new("loans.not_found", ApplicationErrorCategory.NotFound);
}

// GOOD: used by reference, with the value for the {id} placeholder
return CreateFailure<RenewLoanResponse>(
    LoanErrorDefinitions.NotFound,
    new Dictionary<string, string> { ["id"] = loanId.Value.ToString(CultureInfo.InvariantCulture) });

// BAD: the same code written out again where it is used
return CreateFailure<RenewLoanResponse>(new ApplicationErrorDefinition("loans.not_found", ApplicationErrorCategory.NotFound));
```

Introducing another category that clients can act on is a change to the public contract: design its response explicitly, and include it in the startup check.

**Checked by:** review; the startup check rejects a client-visible definition without a message (`ER-011`)

---

<a id="ER-003"></a>

## ER-003 Give every outcome the user can act on a stable public code and a helpful message

Distinguish outcomes the user can act on from failures the user cannot resolve. For invalid input, a missing record, or an expected workflow restriction, explain enough for the client to guide the user, and give the outcome a stable public code. Clients decide what to show from the code, never by matching the English message, so a code is never renamed or reused once it is published.

```jsonc
// BAD: nothing the user or the client can act on
{ "title": "Error", "detail": "Something went wrong.", "code": "error" }

// GOOD
{ "title": "Loan not found", "detail": "No loan was found with identifier 77.", "code": "loans.not_found" }
```

**Checked by:** review; the maintainer accepts every new or changed public code and message (a one-way decision)

---

<a id="ER-004"></a>

## ER-004 Map each error category to its fixed HTTP status

The category decides the status, in one place in Api. A feature never chooses a status code, so the same kind of failure gets the same status everywhere.

| Category | HTTP status |
| --- | --- |
| Validation: the request is invalid | 400 |
| Not found | 404 |
| Method not allowed: the address exists but does not accept the request method | 405 |
| A dependency returned a response that cannot be used | 502 |
| A dependency is unavailable | 503 |
| Authentication to a dependency failed | 500 |
| Unexpected failure | 500 |

```csharp
// BAD: an endpoint picks its own status
return Results.StatusCode(409);

// GOOD: the endpoint hands the result to the one mapper, which applies the table
return mapper.Map(result);
```

**Checked by:** tests of the mapper, and review

---

<a id="ER-005"></a>

## ER-005 Return errors as Problem Details with `code` and `traceId`, and without `type` or `instance`

Every error body has the same shape and the content type `application/problem+json`. It carries the two extensions `code` and `traceId`. It leaves out the `type` and `instance` links, which would point nowhere.

```jsonc
// GOOD: HTTP 404, Content-Type: application/problem+json
{
  "status": 404,
  "title": "Loan not found",
  "detail": "No loan was found with identifier 77.",
  "code": "loans.not_found",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"
}
```

```jsonc
// BAD: a shape of its own, no code, no trace identifier
{ "error": "Loan not found" }
```

Error bodies are built in one place in Api, not by hand in an endpoint.

**Checked by:** tests of the error responses, and review

---

<a id="ER-006"></a>

## ER-006 Mask every server failure

A response with status 500, 502, or 503 tells the client only that something failed and gives a trace identifier. It always has the title `Internal error` and the public code `internal_error`, has no `detail`, and includes the request's `traceId`. It never reveals a provider's name, an internal error code, an authentication diagnosis, an exception, or any other technical explanation: that information helps an attacker and is of no use to staff.

```jsonc
// GOOD: HTTP 503
{ "status": 503, "title": "Internal error", "code": "internal_error", "traceId": "00-4bf9…-00" }
```

```jsonc
// BAD: names the provider, the internal code, and the diagnosis
{ "status": 500, "title": "Koha authentication failed", "detail": "OAuth token request returned 401", "code": "loans.renew.authentication_failed" }
```

The policy is the same for a failure that Application returned and for an unhandled exception, and the same in Development and in Production. The masked response is defined once, in Api. Errors the user can act on keep their own title, detail, and code (`ER-003`).

**Checked by:** tests of the error responses, and review

---

<a id="ER-007"></a>

## ER-007 Log the real cause with the same trace identifier

Because the response hides the cause (`ER-006`), the server log must hold it, findable from what the client was given. For a recognized server failure, log its internal error code. For an unexpected failure, log the exception. In both cases log the same `traceId` that the response carries.

```csharp
// BAD: the cause is logged, but nothing links it to the response the client received
logger.LogError("Renewal failed");

// GOOD
logger.LogError("Application failure {ErrorCode}. TraceId: {TraceId}", error.Code, traceId);
logger.LogError(exception, "Unhandled API failure. TraceId: {TraceId}", traceId);
```

Internal error definitions exist for this purpose. They stay defined even though their codes are never shown to a client.

**Checked by:** tests of the error responses, and review

---

<a id="ER-008"></a>

## ER-008 Never put a secret or a token in a log or a response

Credentials and access tokens are kept out of every diagnostic output. A log is read by more people, and kept for longer, than the secret is meant to live. The same care applies to patron personal data: log and return only what the governing spec's "Security and privacy" section allows.

```csharp
// BAD
logger.LogError("Token request failed for {ClientId} / {ClientSecret}", options.ClientId, options.ClientSecret);
logger.LogWarning("Koha request failed: {Headers}", request.Headers);          // includes the Authorization header

// GOOD
logger.LogError("Koha OAuth token acquisition failed with status {StatusCode}", exception.StatusCode);
```

**Checked by:** review; the maintainer inspects real logs during manual acceptance

---

<a id="ER-009"></a>

## ER-009 Treat caller cancellation as cancellation, not as a failure

When the client abandons a request, the work stops and the cancellation travels up unchanged. It is not converted into a failure result, not answered as an internal error, and not logged as one. A deadline that the application itself imposed is different: when it expires, that is a dependency failure.

```csharp
// BAD: every cancellation becomes "unavailable", including the caller's own
catch (OperationCanceledException)
{
    return RenewLoanGatewayResult.Failed(RenewLoanOutcome.Unavailable);
}

// GOOD: only the application's own deadline is a failure; the caller's cancellation propagates
catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
{
    return RenewLoanGatewayResult.Failed(RenewLoanOutcome.Unavailable);
}
```

**Checked by:** tests that cancel a request, and review

---

<a id="ER-010"></a>

## ER-010 Translate provider failures once, at the gateway boundary

An adapter translates the provider's recognized failures into Application outcomes at the boundary, in one named place that reads like a table. The use case then selects the Application result from the outcome. Translation scattered over several methods or layers cannot be reviewed as a whole, and provider details leak into Application (`AR-007`).

```csharp
// GOOD: one place, readable as a table. The values are an illustration; the governing spec states the real mapping.
private static RenewLoanOutcome Translate(int statusCode) => statusCode switch
{
    404 => RenewLoanOutcome.NotFound,
    401 or 403 => RenewLoanOutcome.AuthenticationFailed,
    429 or >= 500 => RenewLoanOutcome.Unavailable,
    _ => RenewLoanOutcome.InvalidResponse
};
```

```csharp
// BAD: the use case inspects a provider status code
if (gatewayResult.HttpStatus == 404)
```

**Checked by:** review

---

<a id="ER-011"></a>

## ER-011 Keep client-visible messages in the resource file, and check them at startup

The text shown to clients lives in one file, `Minioasis.Api/Resources/errors.en.json`, keyed by error code. Only English is supported today. The file contains exactly the client-visible definitions from the registry: no more and no fewer.

The application checks this when it starts, and refuses to start on any inconsistency, so that a broken error definition or message fails before a request depends on it. Startup rejects:

- a blank or duplicate error code, a null definition, an invalid category, or an empty registry;
- a client-visible definition without a message;
- a message for an internal (server-failure) error;
- an unknown, duplicate, malformed, null, or incomplete message.

```jsonc
// Minioasis.Api/Resources/errors.en.json
{
  "loans.not_found": {
    "title": "Loan not found",
    "detail": "No loan was found with identifier {id}."
  }
}
```

```csharp
// BAD: English text for the client written in code
return Results.Json(new { title = "Loan not found", detail = "No such loan." }, statusCode: 404);
```

Do not weaken or bypass the startup check.

**Checked by:** tool (the startup check and its tests)
