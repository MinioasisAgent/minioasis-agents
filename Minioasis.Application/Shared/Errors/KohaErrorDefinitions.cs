namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// The three failures of the connection to Koha. Every capability that calls Koha uses these; they are
/// logged only, never shown to a client.
/// </summary>
public static class KohaErrorDefinitions
{
    public static readonly ApplicationErrorDefinition AuthenticationFailed =
        new("koha.authentication_failed", ApplicationErrorCategory.DependencyAuthenticationFailed);

    public static readonly ApplicationErrorDefinition InvalidResponse =
        new("koha.invalid_response", ApplicationErrorCategory.DependencyInvalidResponse);

    public static readonly ApplicationErrorDefinition Unavailable =
        new("koha.unavailable", ApplicationErrorCategory.DependencyUnavailable);
}
