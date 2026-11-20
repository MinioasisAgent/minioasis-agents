using Minioasis.Application.Shared.Outcomes;
using Minioasis.Koha.Authentication.Exceptions;
using Minioasis.Koha.Shared.Clients;
using Minioasis.Koha.Shared.Exceptions;

namespace Minioasis.Koha.Shared.Mappers;

/// <summary>
/// The shared translation of a failed Koha call into Application's <see cref="DependencyFailureOutcome"/> (spec 001,
/// R20 and D8; <c>ER-010</c>). A capability's adapter asks it about every failure it does not claim for itself.
/// </summary>
/// <remarks>
/// At the call step the adapter passes Koha's status: from the reply, or from the generated client's exception. Each
/// generated client has its own exception type, so the adapter reads <c>StatusCode</c> from it and never passes the
/// exception on, logs it, or wraps it: its message and text hold Koha's body (R23, <c>AR-007</c>). At either step the
/// adapter passes the documented exception it caught (<see cref="KohaClientNames"/>). The caller's cancellation is
/// not a failure and never comes here (<c>ER-009</c>).
/// </remarks>
public static class KohaProtocolFailureMapper
{
    /// <summary>
    /// R20, call step: 401 and 403 are an authentication failure; 429 and 500 to 599 are "unavailable"; every other
    /// status is an unusable response, including a 2xx that reaches an adapter as a failure (a status the contract
    /// does not define, or a body that cannot be parsed or does not match the contract).
    /// </summary>
    public static DependencyFailureOutcome Map(int kohaStatus) => kohaStatus switch
    {
        401 or 403 => DependencyFailureOutcome.AuthenticationFailed,
        429 or (>= 500 and <= 599) => DependencyFailureOutcome.Unavailable,
        _ => DependencyFailureOutcome.InvalidResponse,
    };

    /// <summary>
    /// R20: a refused token request is an authentication failure; Koha unreachable at either step (a 429 or 5xx at the
    /// token step, a network error, or the deadline) is "unavailable".
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="exception"/> is not one that the Koha clients document, such as a cancellation.
    /// </exception>
    public static DependencyFailureOutcome Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            KohaAuthenticationException => DependencyFailureOutcome.AuthenticationFailed,
            KohaUnavailableException or HttpRequestException or IOException => DependencyFailureOutcome.Unavailable,
            _ => throw new ArgumentException(
                "Not a failure that the Koha clients document; a cancellation propagates instead.",
                nameof(exception)),
        };
    }
}
