- **Access:** Level 2
- **Last Updated:** 2026-11-22

# Architecture and naming

Rules `AR-001` to `AR-013`. The format, and the meaning of the made-up "renew a loan" examples, are explained in [`README.md`](README.md).

## AR-001 Give each project one responsibility

The three projects separate what the application does, how it is offered over HTTP, and how it talks to Koha. Each can then change without spreading provider or transport details through the others. Choose a component's home by the responsibility it owns.

| Project | Responsibility |
| --- | --- |
| `Minioasis.Application` | Staff use cases, business models and rules, validation, the interfaces it needs from external systems, results, and stable typed error definitions |
| `Minioasis.Api` | HTTP endpoints, request and response mapping, Problem Details, English error text, and wiring the application together |
| `Minioasis.Koha` | Implementations of Application's gateway interfaces, the Koha protocol, generated clients, OAuth, and provider-specific translation |

| You are writing… | It belongs in |
| --- | --- |
| The rule that decides whether a loan may be renewed | `Minioasis.Application` |
| The HTTP status and message for "loan not found" | `Minioasis.Api` |
| The Koha path and JSON for a renewal | `Minioasis.Koha` |

The projects stay at the repository root. A new project or layer is a structural change and needs the maintainer's acceptance.

---

## AR-002 Point dependencies inward: Api and Koha depend on Application, Application depends on neither

Application must be understandable and testable without HTTP and without Koha. If it referenced either project, transport and provider details would leak into the business rules, and every test of a rule would need a web host or a fake Koha.

Api references Application and Koha. Koha references Application. Application references neither. This direction changes only through an approved brief.

```xml
<!-- BAD: in Minioasis.Application.csproj -->
<ProjectReference Include="../Minioasis.Koha/Minioasis.Koha.csproj" />

<!-- GOOD: in Minioasis.Koha.csproj -->
<ProjectReference Include="../Minioasis.Application/Minioasis.Application.csproj" />
```

**Checked by:** the compiler (a project cannot use what it does not reference), and review of every change to a `.csproj` file

---

## AR-003 Keep HTTP, provider, and generated types out of Application

Application states the business meaning of an operation. It must not mention ASP.NET or HTTP types, provider URLs, or generated DTOs, even where a package reference would allow it. A use case that returns an HTTP result can only be called from an endpoint, and one that accepts a Koha DTO breaks whenever the Koha contract changes.

```csharp
// BAD: an HTTP result type and a generated Koha type in a use case
public Task<IResult> ExecuteAsync(Checkout kohaCheckout, CancellationToken cancellationToken)

// GOOD: Application's own request and result types
public Task<ApplicationResult<RenewLoanResponse>> ExecuteAsync(
    RenewLoanRequest request,
    CancellationToken cancellationToken = default)
```

---

## AR-004 Keep endpoints thin

An endpoint translates an HTTP request into one Application operation and maps the result back to HTTP. A decision made inside an endpoint cannot be tested without a web host and is skipped by every other caller of the use case.

```csharp
// BAD: a business rule and a status code decided in the endpoint
private static async Task<IResult> RenewLoanAsync(string id, IRenewLoanUseCase useCase, CancellationToken cancellationToken)
{
    if (!int.TryParse(id, out _))
    {
        return Results.BadRequest();
    }

    var result = await useCase.ExecuteAsync(new RenewLoanRequest(id), cancellationToken);
    return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
}

// GOOD: translate in, call, map out
private static async Task<IResult> RenewLoanAsync(
    string id,
    IRenewLoanUseCase useCase,
    IApplicationResultHttpMapper mapper,
    CancellationToken cancellationToken) =>
    mapper.Map(await useCase.ExecuteAsync(new RenewLoanRequest(id), cancellationToken));
```

---

## AR-005 Wire the application together in Api only

Api is the composition root: the one place that connects interfaces to implementations through dependency injection. Each project offers one registration method for its own services, and Api's startup calls it. When a component builds its own dependencies, they cannot be replaced in a test, and their configuration ends up in several places.

```csharp
// BAD: a use case that constructs its own gateway
var gateway = new KohaRenewLoanAdapter(new HttpClient(), options, logger);

// GOOD: Api's startup calls each project's registration method; components receive what they need
builder.Services.AddApplication();
builder.Services.AddKohaIntegration(builder.Configuration);
```

---

## AR-006 Declare external-system interfaces in Application, in Application's own types

Application defines what it needs from an external system; the integration project supplies it. The interface therefore lives in Application, and its inputs and outputs are Application-owned types. If the interface lived in the integration project, or mentioned its types, Application would depend on the provider.

```csharp
// BAD: declared in Minioasis.Koha, or shaped by the provider
public interface IKohaCheckoutClient
{
    Task<HttpResponseMessage> PostRenewalAsync(int checkoutId);
}

// GOOD: Minioasis.Application/Loans/Abstractions/IRenewLoanGateway.cs
public interface IRenewLoanGateway
{
    Task<RenewLoanGatewayResult> RenewLoanAsync(LoanId loanId, CancellationToken cancellationToken = default);
}
```

---

## AR-007 Keep generated types and provider exceptions inside the integration project

Generated DTOs and provider exceptions change when the provider's contract changes. An adapter translates identifiers and models to the provider's protocol, makes the call, and translates the provider's data and recognized failures back into Application results. Nothing generated and no provider exception crosses that boundary.

```csharp
// BAD: a generated type in a signature that Application sees
public async Task<Checkout> RenewLoanAsync(LoanId loanId, CancellationToken cancellationToken)

// BAD: a provider exception left to escape into Application
var checkout = await client.RenewCheckoutAsync(loanId.Value, cancellationToken);   // may throw KohaServiceException

// GOOD: translate both at the boundary
try
{
    var checkout = await client.RenewCheckoutAsync(loanId.Value, cancellationToken);
    return RenewLoanGatewayResult.Renewed(new RenewLoanResponse(checkout.Due_date));
}
catch (KohaServiceException exception)
{
    return RenewLoanGatewayResult.Failed(Translate(exception.StatusCode));
}
```

Generated types stay in `Minioasis.Koha/Generated`.

---

## AR-008 Organise code by feature first, then by role

Organise handwritten code first by feature or subdomain, then by technical responsibility. Everything about one capability is then found together, and each component's role is visible from its folder.

```text
Minioasis.Application/<Feature>/Requests/<Operation>Request.cs
Minioasis.Application/<Feature>/Responses/<Operation>Response.cs
Minioasis.Application/<Feature>/UseCases/<Operation>UseCase.cs
Minioasis.Application/<Feature>/Abstractions/I<Capability>Gateway.cs
Minioasis.Api/<Feature>/Endpoints/<Feature>Endpoints.cs
Minioasis.Koha/<Feature>/Adapters/Koha<Capability>Adapter.cs
```

```text
BAD    Minioasis.Application/UseCases/RenewLoanUseCase.cs        (role first)
BAD    Minioasis.Koha/Helpers/LoanStuff.cs                       (no feature, no role)
GOOD   Minioasis.Application/Loans/UseCases/RenewLoanUseCase.cs
```

Use `Shared/<Role>` for infrastructure used across features, and `Authentication/<Role>` for authentication concerns. Create a folder when it has code to contain, not before.

---

## AR-009 Move code to `Shared/` only after real reuse

Keep a feature's behavior with the feature. Extract shared infrastructure when demonstrated reuse justifies it: when a second feature actually needs the same thing. Code extracted for an imagined second user gets the wrong shape, and the first real second user then has to work around it.

```text
BAD    A new Shared/Validation/IdentifierRules.cs, written for the one feature that uses it
GOOD   The rule stays in the feature. When a second feature needs the same rule, move it to Shared/ in that change.
```

---

## AR-010 Put one top-level type in each file, and make namespaces follow folders

Every production type, and every reusable shared-test type, has its own file named after the type. The namespace is the folder path. A reader can then find any type from its name alone.

```csharp
// File: Minioasis.Application/Loans/UseCases/RenewLoanUseCase.cs
namespace Minioasis.Application.Loans.UseCases;

internal sealed class RenewLoanUseCase(IRenewLoanGateway gateway) : IRenewLoanUseCase
```

```text
BAD    RenewLoanUseCase.cs that also declares RenewLoanRequest and RenewLoanResponse
```

Private nested implementation types and small test helpers may stay with their owner. Generated code follows its generator's layout and naming.

---

## AR-011 Name a type for its concept and its role

A name states the business concept or capability and the technical responsibility. A reader should know what a type is for without opening it. Use these conventions wherever the responsibility applies.

| Responsibility | Naming convention |
| --- | --- |
| Input to a use case | `<Operation>Request` |
| Successful output data from a use case | `<Operation>Response` |
| Success or failure container | `<Scope>Result` |
| Enumerated operation outcomes | `<Operation>Outcome` |
| Identifier or value object | The domain concept it represents, with `Id` for an identifier |
| Application operation | `<Operation>UseCase` |
| External-system capability interface | `I<Capability>Gateway` |
| Provider-specific implementation | `<Provider><Capability>Adapter` |
| HTTP route registration | `<Feature>Endpoints` |
| Conversion between representations | `<Source><Target>Mapper` |
| Request pipeline behavior | `<Purpose>Handler` |
| Resource acquisition | `<Resource>Provider` |
| Configuration values | `<Integration>Options` |
| Object construction | `<CreatedType>Factory` |
| Reusable error definitions | `<Feature>ErrorDefinitions` |
| Error occurrence | `<Scope>Error` |
| Exception | `<Failure>Exception` |

Prefix an interface with `I` and name it after its capability.

```text
BAD    LoanService, RenewHandlerImpl, IKoha
GOOD   RenewLoanUseCase, IRenewLoanGateway, KohaRenewLoanAdapter, LoanEndpoints
```

---

## AR-012 Keep request, response, result, and outcome distinct

These four are different things, and the difference must stay visible in signatures and file names.

- A **request** carries the input to an operation.
- A **response** carries its successful data.
- A **result** describes success or failure, and may contain that response or an error.
- An **outcome** lists the things that can happen in an operation.

```csharp
// BAD: one type that is input, output, and status at once
public sealed record RenewLoan(string LoanId, DateOnly? DueDate, bool Failed, string? Message);

// GOOD: each role has its own type
public sealed record RenewLoanRequest(string LoanId);
public sealed record RenewLoanResponse(DateOnly DueDate);
Task<ApplicationResult<RenewLoanResponse>> ExecuteAsync(RenewLoanRequest request, CancellationToken cancellationToken = default);
```

---

## AR-013 Prefer a specific role name to a catch-all

A catch-all name such as `Helper`, `Manager`, `Utils`, `Service`, or `Common` says nothing about responsibility, so unrelated code collects in it. If no role in `AR-011` fits, name the one thing the type does.

```text
BAD    KohaHelper, LoanManager, CommonUtils
GOOD   KohaCheckoutLoanMapper      (converts a Koha checkout into a loan)
GOOD   LoanDueDatePolicy           (decides the due date of a renewed loan)
```
