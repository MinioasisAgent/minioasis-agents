namespace Minioasis.Koha.Shared.Exceptions;

/// <summary>
/// Koha could not be reached: a network error, Koha answered 429 or a status from 500 to 599, or the deadline of one
/// Koha interaction expired (R18, R21). This is the "dependency unavailable" row of spec 001, R20 (D6,
/// <c>EX-011</c>, <c>EX-013</c>).
/// </summary>
/// <remarks>
/// It carries Koha's status when Koha replied, and the network error when there was one: never Koha's body, and
/// never the generated client's exception, whose message and text hold that body (R23).
/// </remarks>
public sealed class KohaUnavailableException : Exception
{
    private const string UnavailableMessage = "Koha could not be reached.";

    /// <summary>The deadline expired before Koha answered in full (R21; <c>EX-011</c>).</summary>
    public KohaUnavailableException()
        : base(UnavailableMessage)
    {
    }

    /// <summary>Koha replied with <paramref name="kohaStatus"/>: 429, or a status from 500 to 599.</summary>
    public KohaUnavailableException(int kohaStatus)
        : base(UnavailableMessage)
    {
        KohaStatus = kohaStatus;
    }

    /// <summary>
    /// Koha did not reply: <paramref name="networkError"/> is the <see cref="HttpRequestException"/> or the
    /// <see cref="IOException"/> that ended the exchange.
    /// </summary>
    public KohaUnavailableException(Exception networkError)
        : base(UnavailableMessage, networkError)
    {
    }

    /// <summary>Koha's HTTP status, or null when Koha did not reply. A number, so it is safe to log (R20, R23).</summary>
    public int? KohaStatus
    {
        get;
    }
}
