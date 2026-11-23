using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minioasis.Application.Items.Abstractions;
using Minioasis.Koha.Authentication.Providers;
using Minioasis.Koha.Shared.Http;

namespace Minioasis.Koha.Tests.Shared.TestDoubles;

/// <summary>
/// The Koha integration as Api registers it, with stub transports in place of the network, a fake clock, and
/// a log that the test can read.
/// </summary>
internal sealed class KohaTestHost : IDisposable
{
    public const string ClientId = "test-client";
    public const string ClientSecret = "test-secret-value";

    private readonly ServiceProvider services;

    public KohaTestHost(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? oauth = null,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? api = null,
        string baseUrl = "https://koha.test",
        string clientId = ClientId,
        string clientSecret = ClientSecret)
    {
        OAuth = new StubHttpMessageHandler(oauth ?? ((_, _) => Task.FromResult(KohaResponses.Token())));
        Api = new StubHttpMessageHandler(api ?? ((_, _) => Task.FromResult(KohaResponses.Item(12, 7, "BC-1", "QA 76"))));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MINIOASIS_KOHA_BASE_URL"] = baseUrl,
                ["MINIOASIS_KOHA_CLIENT_ID"] = clientId,
                ["MINIOASIS_KOHA_CLIENT_SECRET"] = clientSecret,
            })
            .Build();

        var collection = new ServiceCollection();
        collection.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(Logs));
        collection.AddSingleton<TimeProvider>(Time);
        collection.AddKohaIntegration(configuration);
        collection.AddHttpClient(KohaClientNames.OAuth).ConfigurePrimaryHttpMessageHandler(() => OAuth);
        collection.AddHttpClient(KohaClientNames.Api).ConfigurePrimaryHttpMessageHandler(() => Api);
        services = collection.BuildServiceProvider(validateScopes: true);
    }

    public StubHttpMessageHandler OAuth
    {
        get;
    }

    public StubHttpMessageHandler Api
    {
        get;
    }

    public FakeTimeProvider Time { get; } = new();

    public CapturingLoggerProvider Logs { get; } = new();

    public IServiceProvider Services => services;

    public KohaAccessTokenProvider Tokens => services.GetRequiredService<KohaAccessTokenProvider>();

    public IGetItemGateway CreateGateway() => services.CreateScope().ServiceProvider.GetRequiredService<IGetItemGateway>();

    public void Dispose() => services.Dispose();
}
