using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minioasis.Application.Items.Abstractions;
using Minioasis.Koha.Authentication.Providers;
using Minioasis.Koha.Shared.Http;
using Minioasis.Koha.Shared.Options;
using Minioasis.Koha.Tests.Shared.TestDoubles;

namespace Minioasis.Koha.Tests.TestKoha;

/// <summary>
/// The Koha integration as Api registers it, pointed at the test Koha, with the Cloudflare Access headers added
/// and every exchange recorded.
/// </summary>
internal sealed class TestKohaHost : IDisposable
{
    private readonly ServiceProvider services;

    public TestKohaHost(TestKohaSettings settings, string? clientSecret = null, TokenRewritingHandler? rewriting = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [KohaOptions.BaseUrlSetting] = settings.BaseUrl,
                [KohaOptions.ClientIdSetting] = settings.ClientId,
                [KohaOptions.ClientSecretSetting] = clientSecret ?? settings.ClientSecret,
            })
            .Build();

        var collection = new ServiceCollection();
        collection.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(Logs));
        collection.AddKohaIntegration(configuration);

        var oauth = collection.AddHttpClient(KohaClientNames.OAuth).AddHttpMessageHandler(() => OAuth);
        if (rewriting is not null)
        {
            oauth.AddHttpMessageHandler(() => rewriting);
        }

        oauth.AddHttpMessageHandler(() => new CloudflareAccessHandler(settings));
        collection.AddHttpClient(KohaClientNames.Api)
            .AddHttpMessageHandler(() => Api)
            .AddHttpMessageHandler(() => new CloudflareAccessHandler(settings));

        services = collection.BuildServiceProvider(validateScopes: true);
        services.GetRequiredService<IStartupValidator>().Validate();
    }

    public RecordingHandler OAuth { get; } = new();

    public RecordingHandler Api { get; } = new();

    public CapturingLoggerProvider Logs { get; } = new();

    public KohaAccessTokenProvider Tokens => services.GetRequiredService<KohaAccessTokenProvider>();

    public IGetItemGateway CreateGateway() => services.CreateScope().ServiceProvider.GetRequiredService<IGetItemGateway>();

    /// <summary>An HTTP client for set-up and clean-up calls, with the application's handlers and the test's.</summary>
    public HttpClient CreateApiClient()
    {
        var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(KohaClientNames.Api);
        client.BaseAddress = services.GetRequiredService<IOptions<KohaOptions>>().Value.ApiBaseAddress;
        return client;
    }

    public void Dispose() => services.Dispose();
}
