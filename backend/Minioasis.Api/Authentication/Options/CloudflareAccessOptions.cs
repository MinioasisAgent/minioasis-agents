namespace Minioasis.Api.Authentication.Options;

/// <summary>
/// The Cloudflare Access application in front of Minioasis and the one staff email, read from the three
/// MINIOASIS_CLOUDFLARE_ACCESS_* settings (brief 002, part 3). The values are never logged.
/// </summary>
internal sealed class CloudflareAccessOptions
{
    public const string TeamDomainSetting = "MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN";
    public const string AudienceSetting = "MINIOASIS_CLOUDFLARE_ACCESS_AUDIENCE";
    public const string EmailSetting = "MINIOASIS_CLOUDFLARE_ACCESS_EMAIL";

    private const string KeySetPath = "/cdn-cgi/access/certs";

    public string? TeamDomain
    {
        get; set;
    }

    public string? Audience
    {
        get; set;
    }

    public string? Email
    {
        get; set;
    }

    /// <summary>
    /// The issuer that every token must carry: the origin of the team domain, which is the scheme, the host in lower
    /// case, and the port when it is not 443, without a trailing <c>/</c>. Valid only after validation.
    /// </summary>
    public string Issuer => new Uri(TeamDomain!, UriKind.Absolute).GetLeftPart(UriPartial.Authority);

    /// <summary>The address at which Cloudflare publishes the team's signing keys. Valid only after validation.</summary>
    public string KeySetAddress => Issuer + KeySetPath;
}
