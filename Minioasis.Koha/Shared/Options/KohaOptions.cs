namespace Minioasis.Koha.Shared.Options;

/// <summary>
/// The connection to Koha, read from the three MINIOASIS_KOHA_* settings (EX-005).
/// </summary>
internal sealed class KohaOptions
{
    public const string BaseUrlSetting = "MINIOASIS_KOHA_BASE_URL";
    public const string ClientIdSetting = "MINIOASIS_KOHA_CLIENT_ID";
    public const string ClientSecretSetting = "MINIOASIS_KOHA_CLIENT_SECRET";

    /// <summary>
    /// The one deadline on all Koha work for one request (EX-011), and on one shared token request.
    /// </summary>
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    public string? BaseUrl
    {
        get; set;
    }

    public string? ClientId
    {
        get; set;
    }

    public string? ClientSecret
    {
        get; set;
    }

    /// <summary>
    /// The address of Koha's REST API: the base URL with <c>/api/v1/</c> added. Valid only after validation.
    /// </summary>
    public Uri ApiBaseAddress => new(new Uri(BaseUrl!, UriKind.Absolute), "/api/v1/");
}
