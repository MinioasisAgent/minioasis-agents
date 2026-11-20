namespace Minioasis.Koha.Shared.Options;

/// <summary>
/// The settings of the Koha connection, read from the environment and checked at startup by
/// <see cref="Factories.KohaOptionsFactory"/> (spec 001, R10 to R12).
/// </summary>
/// <remarks>
/// A class, not a record: a record's generated <c>ToString</c> would write the client secret (R13, <c>ER-008</c>).
/// </remarks>
public sealed class KohaOptions
{
    /// <summary>
    /// The absolute base URL of Koha's REST API, ending with <c>/</c>, without query, fragment, or user information.
    /// </summary>
    public required Uri BaseUrl
    {
        get;
        init;
    }

    public required string ClientId
    {
        get;
        init;
    }

    public required string ClientSecret
    {
        get;
        init;
    }

    /// <summary>The deadline for one Koha interaction (R12, R21).</summary>
    public required TimeSpan Deadline
    {
        get;
        init;
    }
}
