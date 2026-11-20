namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// Errors in a request that ASP.NET finds before any capability runs (spec 001, D2).
/// </summary>
[ErrorDefinitions]
public static class RequestErrorDefinitions
{
    /// <summary>No route matches the path (R6).</summary>
    public static readonly ApplicationErrorDefinition NotFound =
        new("not_found", ApplicationErrorCategory.NotFound);

    /// <summary>The route does not accept the method (R7).</summary>
    public static readonly ApplicationErrorDefinition MethodNotAllowed =
        new("method_not_allowed", ApplicationErrorCategory.MethodNotAllowed);

    /// <summary>The request body, or a route or query value, cannot be read (R8, R8a).</summary>
    public static readonly ApplicationErrorDefinition Invalid =
        new("request.invalid", ApplicationErrorCategory.Validation);
}
