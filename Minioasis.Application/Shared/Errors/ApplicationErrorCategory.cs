namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// The kind of an Application failure. Api maps each category to one HTTP status (ER-004).
/// The values start at 1, so that an unset category is undefined and the registry rejects it.
/// </summary>
public enum ApplicationErrorCategory
{
    /// <summary>The request is invalid.</summary>
    Validation = 1,

    /// <summary>The requested resource does not exist.</summary>
    NotFound = 2,

    /// <summary>The address exists but does not accept the request method.</summary>
    MethodNotAllowed = 3,

    /// <summary>A dependency returned a response that cannot be used.</summary>
    DependencyInvalidResponse = 4,

    /// <summary>A dependency is unavailable.</summary>
    DependencyUnavailable = 5,

    /// <summary>Authentication to a dependency failed.</summary>
    DependencyAuthenticationFailed = 6,

    /// <summary>An unexpected failure.</summary>
    Unexpected = 7,
}
