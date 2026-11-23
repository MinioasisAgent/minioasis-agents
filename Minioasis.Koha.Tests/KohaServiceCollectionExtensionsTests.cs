using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minioasis.Application.Items.Abstractions;
using Minioasis.Koha.Authentication.Handlers;
using Minioasis.Koha.Authentication.Providers;
using Minioasis.Koha.Items.Adapters;
using Minioasis.Koha.Shared.Handlers;
using Minioasis.Koha.Shared.Http;
using Minioasis.Koha.Shared.Options;
using Xunit;

namespace Minioasis.Koha.Tests;

public sealed class KohaServiceCollectionExtensionsTests
{
    [Fact]
    public void The_api_client_applies_the_deadline_then_the_bearer_token_then_the_charset_correction()
    {
        using var services = BuildServices();

        var chain = HandlerChain(services, KohaClientNames.Api);

        Assert.Equal(
            [typeof(KohaDeadlineHandler), typeof(KohaBearerHandler), typeof(KohaCharsetCorrectionHandler)],
            chain.Where(IsKohaHandler).Select(handler => handler.GetType()));
        var primary = Assert.IsType<SocketsHttpHandler>(chain[^1]);
        Assert.False(primary.AllowAutoRedirect);
    }

    [Fact]
    public void The_oauth_client_has_no_bearer_handler_and_follows_no_redirect()
    {
        using var services = BuildServices();

        var chain = HandlerChain(services, KohaClientNames.OAuth);

        Assert.Equal([typeof(KohaCharsetCorrectionHandler)], chain.Where(IsKohaHandler).Select(handler => handler.GetType()));
        var primary = Assert.IsType<SocketsHttpHandler>(chain[^1]);
        Assert.False(primary.AllowAutoRedirect);
    }

    [Fact]
    public void Only_the_oauth_client_carries_the_basic_credentials()
    {
        using var services = BuildServices();
        var factory = services.GetRequiredService<IHttpClientFactory>();

        using var oauth = factory.CreateClient(KohaClientNames.OAuth);
        using var api = factory.CreateClient(KohaClientNames.Api);

        Assert.Equal("Basic", oauth.DefaultRequestHeaders.Authorization!.Scheme);
        Assert.Null(api.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public void Resolves_the_get_item_gateway_and_one_shared_token_provider()
    {
        using var services = BuildServices();
        using var scope = services.CreateScope();

        Assert.IsType<KohaGetItemAdapter>(scope.ServiceProvider.GetRequiredService<IGetItemGateway>());
        Assert.Same(
            services.GetRequiredService<KohaAccessTokenProvider>(),
            scope.ServiceProvider.GetRequiredService<KohaAccessTokenProvider>());
        Assert.Same(TimeProvider.System, services.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void Reads_the_three_settings_from_configuration()
    {
        using var services = BuildServices();

        var options = services.GetRequiredService<IOptions<KohaOptions>>().Value;

        Assert.Equal("https://koha.test", options.BaseUrl);
        Assert.Equal("client", options.ClientId);
        Assert.Equal("secret", options.ClientSecret);
    }

    [Fact]
    public void Checks_the_settings_when_the_host_starts()
    {
        using var services = BuildServices(baseUrl: "http://koha.internal");

        var exception = Assert.Throws<OptionsValidationException>(
            () => services.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("MINIOASIS_KOHA_BASE_URL", exception.Message, StringComparison.Ordinal);
    }

    private static bool IsKohaHandler(HttpMessageHandler handler) =>
        handler.GetType().Namespace!.StartsWith("Minioasis.Koha", StringComparison.Ordinal);

    private static List<HttpMessageHandler> HandlerChain(IServiceProvider services, string name)
    {
        var chain = new List<HttpMessageHandler>();
        HttpMessageHandler? handler = services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(name);
        while (handler is not null)
        {
            chain.Add(handler);
            handler = (handler as DelegatingHandler)?.InnerHandler;
        }

        return chain;
    }

    private static ServiceProvider BuildServices(string baseUrl = "https://koha.test")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MINIOASIS_KOHA_BASE_URL"] = baseUrl,
                ["MINIOASIS_KOHA_CLIENT_ID"] = "client",
                ["MINIOASIS_KOHA_CLIENT_SECRET"] = "secret",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKohaIntegration(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }
}
