using Minioasis.Koha.Authentication.Exceptions;
using Minioasis.Koha.Shared.Exceptions;

namespace Minioasis.Koha.Shared.Clients;

/// <summary>
/// The names of the two Koha HTTP clients that <c>AddKohaIntegration</c> registers (<c>EX-007</c>): one for the token
/// request, and one for protected calls.
/// </summary>
/// <remarks>
/// <para>
/// The exception contract of a send through the <see cref="Api"/> client (spec 001, R20 to R22; <c>CD-005</c>). It
/// completes with Koha's reply, whose body has already been read within the deadline, or it throws one of these:
/// </para>
/// <list type="bullet">
/// <item><see cref="KohaAuthenticationException"/>: the token step failed because Koha refused the token request or
/// sent no valid token. <c>KohaStatus</c> is Koha's status.</item>
/// <item><see cref="KohaUnavailableException"/>: Koha could not be reached at the token step (a network error, 429,
/// or a status from 500 to 599, with <c>KohaStatus</c>), or the deadline expired at any step (no
/// <c>KohaStatus</c>).</item>
/// <item><see cref="HttpRequestException"/>: a network error at the call step. A body that broke off while it was
/// read arrives as one, around the <see cref="IOException"/>, because the deadline handler reads the body. R20
/// counts a bare <see cref="IOException"/> as a network error too.</item>
/// <item><see cref="OperationCanceledException"/>: the caller cancelled. It is not a failure (<c>ER-009</c>).</item>
/// </list>
/// <para>
/// Every other outcome is a reply: a 401 after the retry of a <c>GET</c> or to any other method, and every other
/// status, which the generated client turns into its own exception. <c>KohaProtocolFailureMapper</c> translates all
/// of them. The two Koha exceptions carry Koha's status, never its body and never a generated exception (R23).
/// </para>
/// </remarks>
public static class KohaClientNames
{
    /// <summary>
    /// The client for Koha's token endpoint only, used by the token provider. It has the deadline handler and never
    /// attaches a bearer token.
    /// </summary>
    public const string OAuth = "Minioasis.Koha.OAuth";

    /// <summary>
    /// The client for every protected call to Koha (<c>EX-004</c>): the deadline handler, then the bearer handler.
    /// </summary>
    public const string Api = "Minioasis.Koha.Api";
}
