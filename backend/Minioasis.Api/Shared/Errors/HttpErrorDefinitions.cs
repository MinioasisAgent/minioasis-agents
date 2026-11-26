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

    /// <summary>The request carries no Cloudflare Access token, or one that fails a check (brief 002).</summary>
    public static readonly HttpErrorDefinition Unauthenticated = new("unauthenticated", StatusCodes.Status401Unauthorized);

    /// <summary>The token is valid, and its email is not the configured one (brief 002).</summary>
    public static readonly HttpErrorDefinition AccessDenied = new("access_denied", StatusCodes.Status403Forbidden);

    /// <summary>The token and the email are right, and <c>X-Requested-With</c> is not <c>XMLHttpRequest</c> (brief 002).</summary>
    public static readonly HttpErrorDefinition RequestRefused = new("request_refused", StatusCodes.Status403Forbidden);

    /// <summary>Every error of HTTP itself, so that the startup check covers them with Application's registry (ER-011).</summary>
    public static IReadOnlyList<HttpErrorDefinition> All
    {
        get;
    } = [NotFound, MethodNotAllowed, Unauthenticated, AccessDenied, RequestRefused];
}
