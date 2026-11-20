using Microsoft.Extensions.Configuration;
using Minioasis.Koha.Shared.Exceptions;
using Minioasis.Koha.Shared.Factories;
using Xunit;

namespace Minioasis.Koha.Tests.Shared.Factories;

public sealed class KohaOptionsFactoryTests
{
    private const string BaseUrlVariable = "MINIOASIS_KOHA_BASE_URL";
    private const string ClientIdVariable = "MINIOASIS_KOHA_CLIENT_ID";
    private const string ClientSecretVariable = "MINIOASIS_KOHA_CLIENT_SECRET";
    private const string TimeoutVariable = "MINIOASIS_KOHA_TIMEOUT_SECONDS";

    private static readonly string[] RequiredVariables = [BaseUrlVariable, ClientIdVariable, ClientSecretVariable];

    [Fact]
    public void Valid_configuration_gives_the_options()
    {
        var options = KohaOptionsFactory.Create(
            Configuration(
                (BaseUrlVariable, "https://koha.test/api/v1"),
                (ClientIdVariable, "dummy-client-id"),
                (ClientSecretVariable, "dummy-client-secret"),
                (TimeoutVariable, "25")),
            allowInsecureHttp: false);

        Assert.Equal(new Uri("https://koha.test/api/v1/"), options.BaseUrl);
        Assert.Equal("dummy-client-id", options.ClientId);
        Assert.Equal("dummy-client-secret", options.ClientSecret);
        Assert.Equal(TimeSpan.FromSeconds(25), options.Deadline);
    }

    // R10: each required variable, absent (null), empty, or only whitespace.
    [Theory]
    [InlineData(BaseUrlVariable, null)]
    [InlineData(BaseUrlVariable, "")]
    [InlineData(BaseUrlVariable, "   ")]
    [InlineData(ClientIdVariable, null)]
    [InlineData(ClientIdVariable, "")]
    [InlineData(ClientIdVariable, " \t ")]
    [InlineData(ClientSecretVariable, null)]
    [InlineData(ClientSecretVariable, "")]
    [InlineData(ClientSecretVariable, "\t\n")]
    public void Missing_or_blank_variable_fails(string variable, string? value)
    {
        var configuration = ValidSettings();
        configuration[variable] = value;

        var exception = Assert.Throws<KohaConfigurationException>(
            () => KohaOptionsFactory.Create(Configuration(configuration), allowInsecureHttp: false));

        AssertNames(exception, variable);
    }

    // R11: every listed input, in Development (http allowed) and in Production.
    [Theory]
    [InlineData("/api/v1", true, false)]
    [InlineData("/api/v1", false, false)]
    [InlineData("api/v1", false, false)]
    [InlineData("koha", true, false)]
    [InlineData("koha", false, false)]
    [InlineData("not a url at all", false, false)]
    [InlineData("ftp://koha.test/api/v1", true, false)]
    [InlineData("ftp://koha.test/api/v1", false, false)]
    [InlineData("file:///api/v1", true, false)]
    [InlineData("http://koha.test/api/v1", true, true)]
    [InlineData("http://koha.test/api/v1", false, false)]
    [InlineData("https://koha.test/api/v1", true, true)]
    [InlineData("https://koha.test/api/v1", false, true)]
    [InlineData("https://koha.test/api/v1/", true, true)]
    [InlineData("https://koha.test/api/v1/", false, true)]
    [InlineData("HTTPS://koha.test:8443/koha/api/v1", false, true)]
    [InlineData("https://koha.test/api/v1?x=1", true, false)]
    [InlineData("https://koha.test/api/v1?x=1", false, false)]
    [InlineData("https://koha.test/api/v1?", false, false)]
    [InlineData("https://koha.test/api/v1#x", true, false)]
    [InlineData("https://koha.test/api/v1#x", false, false)]
    [InlineData("https://koha.test/api/v1#", false, false)]
    [InlineData("https://u:p@koha.test/api/v1", true, false)]
    [InlineData("https://u:p@koha.test/api/v1", false, false)]
    [InlineData("https://u@koha.test/api/v1", false, false)]
    [InlineData("https://@koha.test/api/v1", false, false)]
    [InlineData("http://u:p@koha.test/api/v1", true, false)]
    public void Base_url_rules(string baseUrl, bool development, bool accepted)
    {
        var configuration = ValidSettings();
        configuration[BaseUrlVariable] = baseUrl;

        if (accepted)
        {
            var options = KohaOptionsFactory.Create(Configuration(configuration), allowInsecureHttp: development);

            Assert.True(options.BaseUrl.IsAbsoluteUri);
            Assert.EndsWith("/", options.BaseUrl.AbsoluteUri, StringComparison.Ordinal);
        }
        else
        {
            var exception = Assert.Throws<KohaConfigurationException>(
                () => KohaOptionsFactory.Create(Configuration(configuration), allowInsecureHttp: development));

            AssertNames(exception, BaseUrlVariable);
        }
    }

    [Fact]
    public void Http_outside_development_says_https_is_required()
    {
        var configuration = ValidSettings();
        configuration[BaseUrlVariable] = "http://koha.test/api/v1";

        var exception = Assert.Throws<KohaConfigurationException>(
            () => KohaOptionsFactory.Create(Configuration(configuration), allowInsecureHttp: false));

        AssertNames(exception, BaseUrlVariable);
        Assert.Contains("https is required", exception.Message, StringComparison.Ordinal);
    }

    // R11: a trailing slash is optional, and both forms give the same base.
    [Theory]
    [InlineData("https://koha.test/api/v1", "https://koha.test/api/v1/")]
    [InlineData("https://koha.test/api/v1/", "https://koha.test/api/v1/")]
    [InlineData("https://koha.test/koha/api/v1", "https://koha.test/koha/api/v1/")]
    [InlineData("https://koha.test", "https://koha.test/")]
    public void Trailing_slash_is_optional(string baseUrl, string expected)
    {
        var configuration = ValidSettings();
        configuration[BaseUrlVariable] = baseUrl;

        var options = KohaOptionsFactory.Create(Configuration(configuration), allowInsecureHttp: false);

        Assert.Equal(expected, options.BaseUrl.AbsoluteUri);
    }

    // R12: the 16 listed values. null means the variable is absent.
    [Theory]
    [InlineData(null, 10)]
    [InlineData("1", 1)]
    [InlineData("10", 10)]
    [InlineData("010", 10)]
    [InlineData("120", 120)]
    [InlineData("0", null)]
    [InlineData("-5", null)]
    [InlineData("+10", null)]
    [InlineData("121", null)]
    [InlineData("10.5", null)]
    [InlineData("ten", null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" 10 ", null)]
    [InlineData("１０", null)]
    [InlineData("99999999999999999999", null)]
    public void Timeout_rules(string? value, int? expectedSeconds)
    {
        var configuration = ValidSettings();
        configuration[TimeoutVariable] = value;

        if (expectedSeconds is { } seconds)
        {
            var options = KohaOptionsFactory.Create(Configuration(configuration), allowInsecureHttp: false);

            Assert.Equal(TimeSpan.FromSeconds(seconds), options.Deadline);
        }
        else
        {
            var exception = Assert.Throws<KohaConfigurationException>(
                () => KohaOptionsFactory.Create(Configuration(configuration), allowInsecureHttp: false));

            AssertNames(exception, TimeoutVariable);
            Assert.Contains("1 to 120", exception.Message, StringComparison.Ordinal);
        }
    }

    // R13: whatever fails, no configured value appears in the message, and there is no inner exception that could
    // carry one. The message, not ToString(), is checked, because the stack trace holds paths and line numbers.
    [Theory]
    [InlineData(BaseUrlVariable, "ftp://marker-host.test/marker-path")]
    [InlineData(BaseUrlVariable, "http://marker-host.test/marker-path")]
    [InlineData(BaseUrlVariable, "https://marker-host.test/marker-path?marker-query=1")]
    [InlineData(BaseUrlVariable, "https://marker-host.test/marker-path#marker-fragment")]
    [InlineData(BaseUrlVariable, "https://marker-user:marker-password@marker-host.test/marker-path")]
    [InlineData(BaseUrlVariable, "marker-not-a-url")]
    [InlineData(ClientIdVariable, "   ")]
    [InlineData(ClientSecretVariable, "   ")]
    [InlineData(TimeoutVariable, "987")]
    [InlineData(TimeoutVariable, "marker-timeout")]
    public void Message_never_contains_the_value(string failing, string value)
    {
        var configuration = new Dictionary<string, string?>
        {
            [BaseUrlVariable] = "https://marker-good-host.test/marker-good-path",
            [ClientIdVariable] = "marker-client-id",
            [ClientSecretVariable] = "marker-client-secret",
            [TimeoutVariable] = "17",
        };
        configuration[failing] = value;

        var exception = Assert.Throws<KohaConfigurationException>(
            () => KohaOptionsFactory.Create(Configuration(configuration), allowInsecureHttp: false));

        AssertNames(exception, failing);
        Assert.DoesNotContain("marker", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("17", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("987", exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void Null_configuration_throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => KohaOptionsFactory.Create(null!, allowInsecureHttp: false));

        Assert.Equal("configuration", exception.ParamName);
    }

    private static Dictionary<string, string?> ValidSettings() => new()
    {
        [BaseUrlVariable] = "https://koha.test/api/v1",
        [ClientIdVariable] = "dummy-client-id",
        [ClientSecretVariable] = "dummy-client-secret",
    };

    private static IConfiguration Configuration(params (string Key, string? Value)[] settings) =>
        Configuration(settings.ToDictionary(setting => setting.Key, setting => setting.Value));

    private static IConfiguration Configuration(IDictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    // The message names the failing variable, and no other required variable.
    private static void AssertNames(KohaConfigurationException exception, string variable)
    {
        Assert.Contains(variable, exception.Message, StringComparison.Ordinal);
        foreach (var other in RequiredVariables.Where(name => name != variable))
        {
            Assert.DoesNotContain(other, exception.Message, StringComparison.Ordinal);
        }
    }
}
