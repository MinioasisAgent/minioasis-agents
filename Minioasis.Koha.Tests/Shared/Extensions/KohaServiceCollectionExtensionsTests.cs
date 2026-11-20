using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Minioasis.Koha.Authentication.Handlers;
using Minioasis.Koha.Authentication.Providers;
using Minioasis.Koha.Shared.Clients;
using Minioasis.Koha.Shared.Exceptions;
using Minioasis.Koha.Shared.Extensions;
using Minioasis.Koha.Shared.Handlers;
using Minioasis.Koha.Shared.Options;
using Minioasis.Koha.Tests.Shared.TestDoubles;
using System.Net;
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

    // TS-005, EX-007, R15: the token provider resolves from the registration as one instance (one cache, one shared
    // request), and sends its request through the OAuth client to the configured base, never through the Api client.
    [Fact]
    public async Task Registers_the_token_provider_on_the_oauth_client()
    {
        var oauth = new StubHttpMessageHandler(() => Token("wired-token", 3600));
        var api = new StubHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var services = new ServiceCollection().AddKohaIntegration(ValidConfiguration(), allowInsecureHttp: false);
        services.AddHttpClient(KohaClientNames.OAuth).ConfigurePrimaryHttpMessageHandler(() => oauth);
        services.AddHttpClient(KohaClientNames.Api).ConfigurePrimaryHttpMessageHandler(() => api);
        using var provider = services.BuildServiceProvider(Validating);

        var tokens = provider.GetRequiredService<KohaAccessTokenProvider>();
        var token = await tokens.GetTokenAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Same(tokens, provider.GetRequiredService<KohaAccessTokenProvider>());
        Assert.Equal("wired-token", token);
        Assert.Equal("https://koha.test/api/v1/oauth/token", Assert.Single(oauth.Requests).Uri?.AbsoluteUri);
        Assert.Empty(api.Requests);
    }

    // TS-005, EX-007, R18, R21, plan "Approach": the OAuth client has the deadline handler and no bearer handler; the
    // API client has the deadline handler, then the bearer handler, so one deadline covers the token step, the call,
    // and the retry. Both primary handlers are SocketsHttpHandler with automatic redirects off, so a 3xx is a final
    // reply (R20) and no request is sent again to another address (R15, R19, EX-010).
    [Theory]
    [InlineData(KohaClientNames.OAuth, new[] { typeof(KohaDeadlineHandler) })]
    [InlineData(KohaClientNames.Api, new[] { typeof(KohaDeadlineHandler), typeof(KohaBearerHandler) })]
    public void Registers_clients_and_handler_order(string name, Type[] expected)
    {
        using var provider = new ServiceCollection()
            .AddKohaIntegration(ValidConfiguration(), allowInsecureHttp: false)
            .BuildServiceProvider(Validating);

        var chain = new List<HttpMessageHandler>();
        HttpMessageHandler? handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(name);
        while (handler is not null)
        {
            chain.Add(handler);
            handler = (handler as DelegatingHandler)?.InnerHandler;
        }

        Assert.Equal(
            expected,
            chain.Select(item => item.GetType()).Where(type => type.Namespace!.StartsWith("Minioasis.", StringComparison.Ordinal)));
        var primary = Assert.IsType<SocketsHttpHandler>(chain[^1]);
        Assert.False(primary.AllowAutoRedirect);
    }

    // Plan, "Approach": TimeProvider.System is registered when no time provider is.
    [Fact]
    public void Registers_the_system_time_provider()
    {
        using var provider = new ServiceCollection()
            .AddKohaIntegration(ValidConfiguration(), allowInsecureHttp: false)
            .BuildServiceProvider(Validating);

        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    // Plan, "Approach": a time provider registered before is kept, and the token provider measures token lifetimes
    // with it (R17).
    [Fact]
    public async Task Keeps_a_registered_time_provider()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero));
        var oauth = new StubHttpMessageHandler(
            (request, _) => Task.FromResult(Token($"token-{request.Index + 1}", 3600)));
        var services = new ServiceCollection().AddSingleton<TimeProvider>(time);
        services.AddKohaIntegration(ValidConfiguration(), allowInsecureHttp: false);
        services.AddHttpClient(KohaClientNames.OAuth).ConfigurePrimaryHttpMessageHandler(() => oauth);
        using var provider = services.BuildServiceProvider(Validating);
        var tokens = provider.GetRequiredService<KohaAccessTokenProvider>();

        Assert.Same(time, provider.GetRequiredService<TimeProvider>());
        Assert.Equal("token-1", await GetTokenAsync(tokens));
        time.Advance(TimeSpan.FromSeconds(3600 - 30));
        Assert.Equal("token-2", await GetTokenAsync(tokens));
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

    private static Task<string> GetTokenAsync(KohaAccessTokenProvider tokens) =>
        tokens.GetTokenAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    private static HttpResponseMessage Token(string accessToken, int lifetimeSeconds) =>
        StubHttpMessageHandler.Reply(
            HttpStatusCode.OK,
            $$"""{"access_token":"{{accessToken}}","token_type":"Bearer","expires_in":{{lifetimeSeconds}}}""");

    private static IConfiguration ValidConfiguration() => Configuration(new Dictionary<string, string?>
    {
        ["MINIOASIS_KOHA_BASE_URL"] = "https://koha.test/api/v1",
        ["MINIOASIS_KOHA_CLIENT_ID"] = "dummy-client-id",
        ["MINIOASIS_KOHA_CLIENT_SECRET"] = "dummy-client-secret",
    });

    private static IConfiguration Configuration(IDictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
}
