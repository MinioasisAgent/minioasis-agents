namespace Minioasis.Koha.Shared.Http;

/// <summary>
/// The two named HTTP clients of EX-007.
/// </summary>
internal static class KohaClientNames
{
    /// <summary>Requests tokens. It has no bearer handler.</summary>
    public const string OAuth = "Minioasis.Koha.OAuth";

    /// <summary>Makes the protected API calls, with the deadline and bearer handlers.</summary>
    public const string Api = "Minioasis.Koha.Api";
}
