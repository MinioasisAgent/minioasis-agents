namespace Minioasis.Api.Shared.Errors;

/// <summary>
/// The errors of HTTP itself that the API answers in the one error shape (ER-004, ER-005).
/// </summary>
internal static class HttpErrorDefinitions
{
    /// <summary>No route matches the path.</summary>
    public static readonly HttpErrorDefinition NotFound = new("not_found", StatusCodes.Status404NotFound);

    /// <summary>The route exists, but not for the request method.</summary>
    public static readonly HttpErrorDefinition MethodNotAllowed = new("method_not_allowed", StatusCodes.Status405MethodNotAllowed);

    /// <summary>Every error of HTTP itself, so that the startup check covers them with Application's registry (ER-011).</summary>
    public static IReadOnlyList<HttpErrorDefinition> All { get; } = [NotFound, MethodNotAllowed];
}
