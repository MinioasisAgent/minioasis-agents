using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minioasis.Koha.Shared.Clients;
using Minioasis.Koha.Shared.Exceptions;
using Minioasis.Koha.Shared.Extensions;
using Minioasis.Koha.Shared.Options;
using Xunit;

namespace Minioasis.Koha.Tests.Shared.Extensions;

public sealed class KohaServiceCollectionExtensionsTests
{
    private static readonly ServiceProviderOptions Validating = new() { ValidateOnBuild = true, ValidateScopes = true };

    // TS-005: the two named clients resolve from the registration. HttpClient.Timeout is infinite on both, because
    // the deadline of R21 is applied by the Koha deadline handler, not by HttpClient (plan, "Files"). An unregistered
    // name would get HttpClient's default of 100 seconds.
    [Theory]
    [InlineData(KohaClientNames.OAuth)]
    [InlineData(KohaClientNames.Api)]
    public void Registers_the_named_client(string name)
    {
        using var provider = new ServiceCollection()
            .AddKohaIntegration(ValidConfiguration(), allowInsecureHttp: false)
            .BuildServiceProvider(Validating);

        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(name);

        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
    }

    [Fact]
    public void The_two_clients_have_different_names()
    {
        Assert.NotEqual(KohaClientNames.OAuth, KohaClientNames.Api);
    }

    [Fact]
    public void Registers_the_checked_options_as_one_instance()
    {
        using var provider = new ServiceCollection()
            .AddKohaIntegration(ValidConfiguration(), allowInsecureHttp: false)
            .BuildServiceProvider(Validating);

        var options = provider.GetRequiredService<KohaOptions>();

        Assert.Same(options, provider.GetRequiredService<KohaOptions>());
        Assert.Equal(new Uri("https://koha.test/api/v1/"), options.BaseUrl);
        Assert.Equal("dummy-client-id", options.ClientId);
        Assert.Equal("dummy-client-secret", options.ClientSecret);
        Assert.Equal(TimeSpan.FromSeconds(10), options.Deadline);
    }

    // Plan, "Files": the options are read while registering, so a bad value fails before any provider is built.
    [Fact]
    public void Invalid_configuration_fails_while_registering()
    {
        var services = new ServiceCollection();
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["MINIOASIS_KOHA_BASE_URL"] = "https://koha.test/api/v1",
            ["MINIOASIS_KOHA_CLIENT_ID"] = "dummy-client-id",
        });

        var exception = Assert.Throws<KohaConfigurationException>(
            () => services.AddKohaIntegration(configuration, allowInsecureHttp: false));

        Assert.Contains("MINIOASIS_KOHA_CLIENT_SECRET", exception.Message, StringComparison.Ordinal);
    }

    // R11: the flag decides whether plain http is allowed.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Insecure_http_flag_is_applied(bool allowInsecureHttp)
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["MINIOASIS_KOHA_BASE_URL"] = "http://koha.test/api/v1",
            ["MINIOASIS_KOHA_CLIENT_ID"] = "dummy-client-id",
            ["MINIOASIS_KOHA_CLIENT_SECRET"] = "dummy-client-secret",
        });

        var register = () => new ServiceCollection().AddKohaIntegration(configuration, allowInsecureHttp);

        if (allowInsecureHttp)
        {
            using var provider = register().BuildServiceProvider(Validating);
            Assert.Equal(new Uri("http://koha.test/api/v1/"), provider.GetRequiredService<KohaOptions>().BaseUrl);
        }
        else
        {
            var exception = Assert.Throws<KohaConfigurationException>(register);
            Assert.Contains("MINIOASIS_KOHA_BASE_URL", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Null_arguments_throw()
    {
        var services = Assert.Throws<ArgumentNullException>(
            () => KohaServiceCollectionExtensions.AddKohaIntegration(null!, ValidConfiguration(), false));
        var configuration = Assert.Throws<ArgumentNullException>(
            () => new ServiceCollection().AddKohaIntegration(null!, false));

        Assert.Equal("services", services.ParamName);
        Assert.Equal("configuration", configuration.ParamName);
    }

    private static IConfiguration ValidConfiguration() => Configuration(new Dictionary<string, string?>
    {
        ["MINIOASIS_KOHA_BASE_URL"] = "https://koha.test/api/v1",
        ["MINIOASIS_KOHA_CLIENT_ID"] = "dummy-client-id",
        ["MINIOASIS_KOHA_CLIENT_SECRET"] = "dummy-client-secret",
    });

    private static IConfiguration Configuration(IDictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
}
