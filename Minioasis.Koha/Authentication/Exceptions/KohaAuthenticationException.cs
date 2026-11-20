namespace Minioasis.Koha.Authentication.Exceptions;

/// <summary>
/// Koha answered the token request but gave no usable token: it refused the request, or its reply failed the checks
/// of spec 001, R16. This is the token-step "authentication to a dependency failed" row of R20 (<c>EX-013</c>).
/// </summary>
/// <remarks>
/// It carries Koha's status only: never Koha's body, and never the generated client's exception, whose message and
/// text hold that body (R23).
/// </remarks>
public sealed class KohaAuthenticationException(int kohaStatus)
    : Exception("The Koha token request was refused, or its reply was not a valid token.")
{
    /// <summary>Koha's HTTP status, 200 for a reply that failed R16. A number, so it is safe to log (R20, R23).</summary>
    public int? KohaStatus
    {
        get;
    } = kohaStatus;
}
