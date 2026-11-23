namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// The kind of an error. Api maps each category to one HTTP status (ER-004).
/// </summary>
public enum ApplicationErrorCategory
{
    /// <summary>The request is invalid.</summary>
    Validation = 1,

    /// <summary>The requested resource does not exist.</summary>
    NotFound,

    /// <summary>The route does not support the request method.</summary>
    MethodNotAllowed,

    /// <summary>A dependency returned a response that cannot be used.</summary>
    DependencyInvalidResponse,

    /// <summary>A dependency is unavailable.</summary>
    DependencyUnavailable,

    /// <summary>Authentication to a dependency failed.</summary>
    DependencyAuthenticationFailed,

    /// <summary>A failure that nothing anticipated.</summary>
    Unexpected,
}
