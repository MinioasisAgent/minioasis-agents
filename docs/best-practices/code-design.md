- **Access:** Level 2
- **Last Updated:** 2026-11-25

# Code design

Rules `CD-001` to `CD-009`. The format, and the meaning of the made-up "renew a loan" examples, are explained in [`README.md`](README.md).

The architecture rules say where code lives. These rules say how a type or a method is shaped. The aim: a reader can say in one sentence what a component is for, and can find every rule in exactly one place.

## CD-001 Give each type and each method one responsibility

Checking input, calling an external system, and translating what came back are three different jobs. A method that does all three is hard to read, hard to test one piece at a time, and grows a long chain of special cases. If the sentence that describes a method needs the word "and", split the method.

```csharp
// BAD: one method checks the input, calls Koha, and translates every failure
public async Task<RenewLoanGatewayResult> RenewLoanAsync(LoanId loanId, CancellationToken cancellationToken)
{
    if (loanId.Value <= 0)
    {
        return RenewLoanGatewayResult.Failed(RenewLoanOutcome.InvalidIdentifier);
    }

    try
    {
        var checkout = await client.RenewCheckoutAsync(loanId.Value, cancellationToken);
        return RenewLoanGatewayResult.Renewed(new RenewLoanResponse(checkout.Due_date));
    }
    catch (KohaServiceException exception) when (exception.StatusCode == 404) { … }
    catch (KohaServiceException exception) when (exception.StatusCode is 401 or 403) { … }
    catch (KohaServiceException exception) when (exception.StatusCode >= 500) { … }
    catch (HttpRequestException) { … }
}

// GOOD: the method calls; mapping and translation each have their own named place
public async Task<RenewLoanGatewayResult> RenewLoanAsync(LoanId loanId, CancellationToken cancellationToken)
{
    try
    {
        var checkout = await client.RenewCheckoutAsync(loanId.Value, cancellationToken);
        return RenewLoanGatewayResult.Renewed(ToResponse(checkout));
    }
    catch (KohaServiceException exception)
    {
        return RenewLoanGatewayResult.Failed(Translate(exception.StatusCode));
    }
}
```

The input check is gone from the good version because of `CD-002` and `CD-003`. The translation is described in `ER-010`.

---

## CD-002 Validate caller input in Application, not in a gateway

Each rule is checked in exactly one place. Rules about what a caller may send, such as format and range, are application rules: they belong in Application, in a value object or a use case. A gateway receives values that are already valid, and never reports invalid caller input back to Application.

When two layers check the same input, the rule has two homes. They drift apart, and nobody can tell from one of them what is actually enforced.

```csharp
// BAD: the Koha adapter decides what a valid identifier is, and reports it back
if (!int.TryParse(request.LoanId, out var kohaId) || kohaId <= 0)
{
    return RenewLoanGatewayResult.Failed(RenewLoanOutcome.InvalidIdentifier);
}

// GOOD: the use case rejects it before any gateway is called
if (!LoanId.TryCreate(request.LoanId, out var loanId))
{
    return CreateFailure<RenewLoanResponse>(LoanErrorDefinitions.InvalidIdentifier);
}

var result = await gateway.RenewLoanAsync(loanId, cancellationToken);
```

Workflow decisions belong in use cases for the same reason.

---

## CD-003 Make identifiers and value objects validate when they are created

If an identifier can hold any text, every piece of code that receives it has to check it again, and one of them will forget. Give the type a creation method that refuses invalid input. Code that receives the type then knows it is valid and does not check it again.

```csharp
// BAD: accepts anything
public readonly record struct LoanId(string Value);

// GOOD: an invalid LoanId cannot exist
public sealed record LoanId
{
    private LoanId(int value)
    {
        Value = value;
    }

    public int Value
    {
        get;
    }

    public static bool TryCreate(string? text, [NotNullWhen(true)] out LoanId? loanId)
    {
        loanId = int.TryParse(text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0
            ? new LoanId(value)
            : null;
        return loanId is not null;
    }
}
```

Prefer a class or a record class. A struct always has a `default` value, which skips the check.

---

## CD-004 Return expected outcomes; throw only for the unexpected and for cancellation

A missing record, invalid input, and a refusal by Koha are expected. Return them explicitly, so that the signature shows them and every caller has to handle them. An exception hides the outcome from the signature and turns an ordinary case into a server error whenever a caller forgets it.

Throw for what should not happen, such as a broken invariant or a programming mistake. Let cancellation travel as `OperationCanceledException`.

```csharp
// BAD: an expected outcome as an exception
if (outcome == RenewLoanOutcome.NotFound)
{
    throw new LoanNotFoundException(loanId);
}

// GOOD: an expected outcome as a result
if (outcome == RenewLoanOutcome.NotFound)
{
    return CreateFailure<RenewLoanResponse>(
        LoanErrorDefinitions.NotFound,
        new Dictionary<string, string> { ["id"] = loanId.Value.ToString(CultureInfo.InvariantCulture) });
}
```

---

## CD-005 Do not steer a neighbouring component with a private exception

A component must not throw an exception type whose only purpose is to be caught and converted by a different component, and must not catch a neighbour's internal exception types. That couples the two invisibly: neither can be understood, changed, or tested without the other.

Report what happened through the component's own contract: a return value, or one documented exception type that its interface owns.

```csharp
// BAD: one handler throws a type that exists only so that another handler can catch it
throw new TokenWaitAbandonedException(exception);                          // in the token handler

catch (TokenWaitAbandonedException exception)                              // in a different handler
{
    throw new SignInFailedException("Sign-in failed.", exception);
}

// GOOD: the component that knows what happened says so itself, in its own contract
public interface ISessionTokenSource
{
    /// <exception cref="SignInFailedException">
    /// A token could not be obtained, for any reason other than the caller cancelling.
    /// </exception>
    Task<string> GetTokenAsync(CancellationToken cancellationToken);
}
```

---

## CD-006 Give each cross-cutting behavior one implementation and one registration

A behavior that applies to many operations, such as enforcing a deadline, attaching a token, or turning an unhandled exception into a response, is implemented once and registered once. Two implementations of the same concern disagree sooner or later, and a reader cannot tell which one is in effect.

```csharp
// BAD: the deadline is applied in the shared handler and again in one adapter
timeout.CancelAfter(options.RequestTimeout);                               // shared handler

using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // an adapter, on its own
var checkout = await client.RenewCheckoutAsync(loanId.Value, deadline.Token);

// GOOD: the shared handler is the only place that applies it; the adapter passes the caller's token on
var checkout = await client.RenewCheckoutAsync(loanId.Value, cancellationToken);
```

---

## CD-007 Remove code that cannot be reached

Every branch and every `catch` must be reachable in the application as it is actually wired together, and must have a test that reaches it. Handling for a failure that cannot arrive is not a safety net. It misleads the reader about what can happen, and it hides the fact that the real failure arrives in another form and may be handled wrongly.

```csharp
// BAD: nothing beneath this call can throw FormatException, so the branch never runs
catch (FormatException)
{
    return RenewLoanGatewayResult.Failed(RenewLoanOutcome.InvalidResponse);
}

// GOOD: handle the failure in the form in which it really arrives, with a test that produces it
catch (KohaServiceException exception)
{
    return RenewLoanGatewayResult.Failed(Translate(exception.StatusCode));
}
```

To find out whether a branch is alive, break it on purpose and run the tests. If nothing fails, either a test is missing or the branch is dead. If a failure arrives in a different form than the code expects, fix the code, not only the test.

---

## CD-008 Add an abstraction only when a second real use needs it

An interface, a factory, an option, or an extension point costs something every time the code is read. Add one when a second real use needs it. Until then, write the plain thing.

```csharp
// BAD: an interface and a factory around one calculation that nothing else will replace
public interface IDueDateFormatter { … }
public sealed class DueDateFormatterFactory { … }

// GOOD: a method, until a second caller needs something different
private static string FormatDueDate(DateOnly dueDate) =>
    dueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
```

This rule does not remove the interfaces that the architecture requires. A gateway interface at the boundary between Application and an integration project (`AR-006`) is required from the first use.

---

## CD-009 Use braces and the `.editorconfig` style

Use braces around the body of every control-flow statement in handwritten production code and tests, including a single-statement `if`, `else`, or loop. Put opening and closing braces on their own lines. One consistent style keeps reviews about substance and keeps diffs small.

```csharp
// BAD
if (result.IsSuccess) return Results.Ok(result.Value);

// GOOD
if (result.IsSuccess)
{
    return Results.Ok(result.Value);
}
```

When you apply this style to existing code, keep the enclosed statements and their order exactly as they were.

**Checked by:** tool (`dotnet format backend/minioasis.slnx --verify-no-changes`, which `scripts/preflight.sh` and CI run). Missing braces are reported only if `.editorconfig` sets `csharp_prefer_braces = true` at warning severity or higher.
