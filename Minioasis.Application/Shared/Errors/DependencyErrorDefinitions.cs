using Minioasis.Application.Shared.Outcomes;

namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// The shared failures of a call to Koha (spec 001, R20). All are masked; their codes are only logged.
/// </summary>
[ErrorDefinitions]
public static class DependencyErrorDefinitions
{
    public static readonly ApplicationErrorDefinition KohaAuthenticationFailed =
        new("koha.authentication_failed", ApplicationErrorCategory.DependencyAuthenticationFailed);

    public static readonly ApplicationErrorDefinition KohaUnavailable =
        new("koha.unavailable", ApplicationErrorCategory.DependencyUnavailable);

    public static readonly ApplicationErrorDefinition KohaInvalidResponse =
        new("koha.invalid_response", ApplicationErrorCategory.DependencyInvalidResponse);

    /// <summary>
    /// Selects the definition for a shared failure of a call to Koha.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is not a defined value.</exception>
    public static ApplicationErrorDefinition ForKohaOutcome(DependencyFailureOutcome outcome) => outcome switch
    {
        DependencyFailureOutcome.AuthenticationFailed => KohaAuthenticationFailed,
        DependencyFailureOutcome.Unavailable => KohaUnavailable,
        DependencyFailureOutcome.InvalidResponse => KohaInvalidResponse,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Undefined dependency failure outcome."),
    };
}
