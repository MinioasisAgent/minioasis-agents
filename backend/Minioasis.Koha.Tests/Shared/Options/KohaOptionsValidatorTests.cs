using Minioasis.Koha.Shared.Options;
using Xunit;

namespace Minioasis.Koha.Tests.Shared.Options;

public sealed class KohaOptionsValidatorTests
{
    private const string Secret = "s3cret-value-never-shown";

    [Theory]
    [InlineData("https://koha.example.org")]
    [InlineData("https://koha.example.org/")]
    [InlineData("https://koha.example.org:8443")]
    [InlineData("http://localhost:8081")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://[::1]:8080/")]
    public void Accepts_a_valid_base_url(string baseUrl)
    {
        var result = new KohaOptionsValidator().Validate(null, Options(baseUrl: baseUrl));

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Theory]
    [InlineData("opac.library.test", "opac.library.test")]
    [InlineData("/staff", "/staff")]
    [InlineData("ftp://opac.library.test", "opac.library.test")]
    [InlineData("file:///etc/staff", "/etc/staff")]
    [InlineData("http://opac.internal", "opac.internal")]
    [InlineData("http://127.0.0.2", "127.0.0.2")]
    [InlineData("http://localhost.library.test", "localhost.library.test")]
    [InlineData("https://staff:hunter2@opac.library.test", "hunter2")]
    [InlineData("https://staff@opac.library.test", "staff@")]
    [InlineData("https://opac.library.test/api/v1", "/api/v1")]
    [InlineData("https://opac.library.test/staff/", "/staff/")]
    [InlineData("https://opac.library.test//", "opac.library.test")]
    [InlineData("https://opac.library.test?debug=1", "debug")]
    [InlineData("https://opac.library.test/?", "opac.library.test")]
    [InlineData("https://opac.library.test#top", "#top")]
    public void Refuses_a_malformed_base_url_naming_the_setting_and_not_the_value(string baseUrl, string revealing)
    {
        var result = new KohaOptionsValidator().Validate(null, Options(baseUrl: baseUrl));

        Assert.True(result.Failed);
        Assert.Contains("MINIOASIS_KOHA_BASE_URL", result.FailureMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(revealing, result.FailureMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuses_a_missing_or_blank_base_url(string? value)
    {
        var result = new KohaOptionsValidator().Validate(null, Options(baseUrl: value));

        Assert.True(result.Failed);
        Assert.Contains("MINIOASIS_KOHA_BASE_URL", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuses_a_missing_or_blank_client_identifier(string? value)
    {
        var result = new KohaOptionsValidator().Validate(null, Options(clientId: value));

        Assert.True(result.Failed);
        Assert.Contains("MINIOASIS_KOHA_CLIENT_ID", result.FailureMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("MINIOASIS_KOHA_CLIENT_SECRET", result.FailureMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuses_a_missing_or_blank_client_secret(string? value)
    {
        var result = new KohaOptionsValidator().Validate(null, Options(clientSecret: value));

        Assert.True(result.Failed);
        Assert.Contains("MINIOASIS_KOHA_CLIENT_SECRET", result.FailureMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("MINIOASIS_KOHA_CLIENT_ID", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Names_every_bad_setting_at_once()
    {
        var result = new KohaOptionsValidator().Validate(null, new KohaOptions());

        Assert.True(result.Failed);
        Assert.Contains("MINIOASIS_KOHA_BASE_URL", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("MINIOASIS_KOHA_CLIENT_ID", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("MINIOASIS_KOHA_CLIENT_SECRET", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://koha.example.org", "https://koha.example.org/api/v1/")]
    [InlineData("https://koha.example.org/", "https://koha.example.org/api/v1/")]
    [InlineData("http://localhost:8081", "http://localhost:8081/api/v1/")]
    public void The_api_base_address_adds_api_v1_after_one_slash(string baseUrl, string expected)
    {
        Assert.Equal(new Uri(expected), Options(baseUrl: baseUrl).ApiBaseAddress);
    }

    private static KohaOptions Options(
        string? baseUrl = "https://koha.example.org",
        string? clientId = "client",
        string? clientSecret = Secret) =>
        new()
        {
            BaseUrl = baseUrl,
            ClientId = clientId,
            ClientSecret = clientSecret
        };
}
