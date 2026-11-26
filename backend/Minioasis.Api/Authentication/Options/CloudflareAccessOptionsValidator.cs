using Microsoft.Extensions.Options;

namespace Minioasis.Api.Authentication.Options;

/// <summary>
/// Refuses settings that are missing, blank, or malformed (brief 002, part 3). A message names the setting and never
/// its value.
/// </summary>
internal sealed class CloudflareAccessOptionsValidator : IValidateOptions<CloudflareAccessOptions>
{
    public ValidateOptionsResult Validate(string? name, CloudflareAccessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.TeamDomain))
        {
            failures.Add($"{CloudflareAccessOptions.TeamDomainSetting} is missing or blank.");
        }
        else if (!IsValidTeamDomain(options.TeamDomain))
        {
            failures.Add(
                $"{CloudflareAccessOptions.TeamDomainSetting} must be an absolute https URL without a user name, " +
                "password, path, query, or fragment, such as https://<team>.cloudflareaccess.com.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add($"{CloudflareAccessOptions.AudienceSetting} is missing or blank.");
        }

        if (string.IsNullOrWhiteSpace(options.Email))
        {
            failures.Add($"{CloudflareAccessOptions.EmailSetting} is missing or blank.");
        }
        else if (!IsValidEmail(options.Email))
        {
            failures.Add(
                $"{CloudflareAccessOptions.EmailSetting} must be one email address: exactly one @ with text on both " +
                "sides, and no space.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsValidTeamDomain(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.UserInfo.Length == 0
        && uri.AbsolutePath == "/"
        && uri.Query.Length == 0
        && uri.Fragment.Length == 0
        && !text.Contains('?', StringComparison.Ordinal)
        && !text.Contains('#', StringComparison.Ordinal);

    private static bool IsValidEmail(string text)
    {
        var at = text.IndexOf('@', StringComparison.Ordinal);
        return at > 0
            && at < text.Length - 1
            && text.IndexOf('@', at + 1) < 0
            && !text.Any(char.IsWhiteSpace);
    }
}
