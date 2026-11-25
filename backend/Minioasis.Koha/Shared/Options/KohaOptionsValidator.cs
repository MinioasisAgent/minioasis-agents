using Microsoft.Extensions.Options;

namespace Minioasis.Koha.Shared.Options;

/// <summary>
/// Refuses settings that are missing, blank, or malformed. A message names the setting and never its value.
/// </summary>
internal sealed class KohaOptionsValidator : IValidateOptions<KohaOptions>
{
    private static readonly string[] LoopbackHosts = ["localhost", "127.0.0.1", "[::1]"];

    public ValidateOptionsResult Validate(string? name, KohaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            failures.Add($"{KohaOptions.BaseUrlSetting} is missing or blank.");
        }
        else if (!IsValidBaseUrl(options.BaseUrl))
        {
            failures.Add(
                $"{KohaOptions.BaseUrlSetting} must be an absolute https URL without a user name, password, path, " +
                "query, or fragment, such as https://koha.example.org. http is accepted only for localhost, " +
                "127.0.0.1, and [::1].");
        }

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            failures.Add($"{KohaOptions.ClientIdSetting} is missing or blank.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            failures.Add($"{KohaOptions.ClientSecretSetting} is missing or blank.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsValidBaseUrl(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri)
        && IsAllowedScheme(uri)
        && uri.UserInfo.Length == 0
        && uri.AbsolutePath == "/"
        && uri.Query.Length == 0
        && uri.Fragment.Length == 0
        && !text.Contains('?', StringComparison.Ordinal)
        && !text.Contains('#', StringComparison.Ordinal);

    private static bool IsAllowedScheme(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps
        || (uri.Scheme == Uri.UriSchemeHttp && LoopbackHosts.Contains(uri.Host, StringComparer.Ordinal));
}
