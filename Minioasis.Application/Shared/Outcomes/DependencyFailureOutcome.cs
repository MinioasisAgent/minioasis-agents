namespace Minioasis.Application.Shared.Outcomes;

/// <summary>
/// The shared failures of a call to a dependency (spec 001, R20). A capability adds only the outcomes specific to it.
/// The values start at 1, so that an unset outcome is undefined.
/// </summary>
public enum DependencyFailureOutcome
{
    /// <summary>Authentication to the dependency failed.</summary>
    AuthenticationFailed = 1,

    /// <summary>The dependency could not be reached, or did not answer in time.</summary>
    Unavailable = 2,

    /// <summary>The dependency returned a response that cannot be used.</summary>
    InvalidResponse = 3,
}
