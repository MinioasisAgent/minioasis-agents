namespace Minioasis.Koha.Shared.Clients;

/// <summary>
/// The names of the two Koha HTTP clients that <c>AddKohaIntegration</c> registers (<c>EX-007</c>): one for the token
/// request, and one for protected calls.
/// </summary>
public static class KohaClientNames
{
    /// <summary>The client for Koha's token endpoint only. It never attaches a bearer token.</summary>
    public const string OAuth = "Minioasis.Koha.OAuth";

    /// <summary>The client for every protected call to Koha (<c>EX-004</c>).</summary>
    public const string Api = "Minioasis.Koha.Api";
}
