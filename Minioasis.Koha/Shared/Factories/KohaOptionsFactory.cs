using Microsoft.Extensions.Configuration;
using Minioasis.Koha.Shared.Exceptions;
using Minioasis.Koha.Shared.Options;
using System.Globalization;

namespace Minioasis.Koha.Shared.Factories;

/// <summary>
/// Reads the four Koha variables and checks them (spec 001, R10 to R12). Every message names the variable and never
/// contains its value or any other configured value (R13), so no value is ever passed to a message or an inner
/// exception.
/// </summary>
public static class KohaOptionsFactory
{
    private const string BaseUrlVariable = "MINIOASIS_KOHA_BASE_URL";
    private const string ClientIdVariable = "MINIOASIS_KOHA_CLIENT_ID";
    private const string ClientSecretVariable = "MINIOASIS_KOHA_CLIENT_SECRET";
    private const string TimeoutVariable = "MINIOASIS_KOHA_TIMEOUT_SECONDS";

    private const int DefaultTimeoutSeconds = 10;
    private const int MinimumTimeoutSeconds = 1;
    private const int MaximumTimeoutSeconds = 120;

    /// <param name="allowInsecureHttp">True only in the <c>Development</c> environment (D5).</param>
    /// <exception cref="KohaConfigurationException">A variable is missing, blank, or invalid.</exception>
    public static KohaOptions Create(IConfiguration configuration, bool allowInsecureHttp)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new KohaOptions
        {
            BaseUrl = ReadBaseUrl(configuration, allowInsecureHttp),
            ClientId = ReadRequired(configuration, ClientIdVariable),
            ClientSecret = ReadRequired(configuration, ClientSecretVariable),
            Deadline = ReadDeadline(configuration),
        };
    }

    private static string ReadRequired(IConfiguration configuration, string variable)
    {
        var value = configuration[variable];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new KohaConfigurationException($"{variable} is missing or blank. It is required.");
        }

        return value;
    }

    // R11: an absolute https URL (http only in Development) without query, fragment, or user information. The result
    // always ends with "/", so that a base with or without a trailing slash gives the same base.
    private static Uri ReadBaseUrl(IConfiguration configuration, bool allowInsecureHttp)
    {
        var value = ReadRequired(configuration, BaseUrlVariable);

        if (!Uri.TryCreate(value, UriKind.Absolute, out var url)
            || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
        {
            throw new KohaConfigurationException(
                $"{BaseUrlVariable} must be an absolute URL whose scheme is https "
                + "(http is allowed only in the Development environment).");
        }

        if (url.Scheme == Uri.UriSchemeHttp && !allowInsecureHttp)
        {
            throw new KohaConfigurationException(
                $"{BaseUrlVariable} uses http: https is required outside the Development environment.");
        }

        if (url.Query.Length > 0
            || url.Fragment.Length > 0
            || url.GetComponents(UriComponents.StrongAuthority, UriFormat.UriEscaped).Contains('@'))
        {
            throw new KohaConfigurationException(
                $"{BaseUrlVariable} must not contain a query string, a fragment, or user information.");
        }

        return url.AbsoluteUri.EndsWith('/') ? url : new Uri(url.AbsoluteUri + "/");
    }

    // R12: absent means the default; otherwise only the ASCII digits 0 to 9, from 1 to 120. NumberStyles.None refuses
    // a sign, whitespace, a decimal point, and non-ASCII digits, and TryParse refuses an overflow.
    private static TimeSpan ReadDeadline(IConfiguration configuration)
    {
        var value = configuration[TimeoutVariable];
        if (value is null)
        {
            return TimeSpan.FromSeconds(DefaultTimeoutSeconds);
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || seconds < MinimumTimeoutSeconds
            || seconds > MaximumTimeoutSeconds)
        {
            throw new KohaConfigurationException(
                $"{TimeoutVariable} must be a whole number of seconds from {MinimumTimeoutSeconds} to "
                + $"{MaximumTimeoutSeconds}, written in the digits 0 to 9, or be left unset for the default of "
                + $"{DefaultTimeoutSeconds}.");
        }

        return TimeSpan.FromSeconds(seconds);
    }
}
