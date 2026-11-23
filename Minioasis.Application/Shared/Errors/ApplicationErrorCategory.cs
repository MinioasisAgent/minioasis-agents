namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// The kind of an error. Api maps each category to one HTTP status (ER-004). Errors of HTTP itself, such as a path
/// that matches no route, are not categories here: Api defines them (AR-003).
/// </summary>
public enum ApplicationErrorCategory
{
    /// <summary>The request is invalid.</summary>
    Validation = 1,

    /// <summary>The requested resource does not exist.</summary>
    NotFound,

    /// <summary>A dependency returned a response that cannot be used.</summary>
    DependencyInvalidResponse,

    /// <summary>A dependency is unavailable.</summary>
    DependencyUnavailable,

    /// <summary>Authentication to a dependency failed.</summary>
    DependencyAuthenticationFailed,

    /// <summary>A failure that nothing anticipated.</summary>
    Unexpected,
}
