using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Minioasis.Koha.Shared.Clients;
using Minioasis.Koha.Shared.Extensions;

namespace Minioasis.Koha.Tests.Shared.TestDoubles;

/// <summary>
/// The Koha connection as <c>AddKohaIntegration</c> registers it, with a <see cref="FakeTimeProvider"/> and with the
/// primary handler of each named client replaced by a fake Koha: <paramref name="tokens"/> for the OAuth client and
/// <paramref name="api"/> for the API client. Everything between the caller and the fake Koha is real.
/// </summary>
public sealed class KohaClientsHarness : IDisposable
{
    public const string BaseUrl = "https://koha.test/api/v1/";
    public const string ClientSecret = "client-secret-5f0c9e";

    /// <summary>The deadline that the harness configures (<c>MINIOASIS_KOHA_TIMEOUT_SECONDS</c>).</summary>
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    public KohaClientsHarness(
        StubHttpMessageHandler tokens,
        StubHttpMessageHandler api,
        RecordingLoggerProvider? logs = null,
        Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MINIOASIS_KOHA_BASE_URL"] = BaseUrl,
                ["MINIOASIS_KOHA_CLIENT_ID"] = "dummy-client-id",
                ["MINIOASIS_KOHA_CLIENT_SECRET"] = ClientSecret,
                ["MINIOASIS_KOHA_TIMEOUT_SECONDS"] = "10",
            })
            .Build();
        var services = new ServiceCollection().AddSingleton<TimeProvider>(Time);
        if (logs is not null)
        {
            services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        }

        configure?.Invoke(services);
        services.AddKohaIntegration(configuration, allowInsecureHttp: false);
        services.AddHttpClient(KohaClientNames.OAuth).ConfigurePrimaryHttpMessageHandler(() => tokens);
        services.AddHttpClient(KohaClientNames.Api).ConfigurePrimaryHttpMessageHandler(() => api);
        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public FakeTimeProvider Time
    {
        get;
    } = new(new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero));

    public ServiceProvider Services
    {
        get;
    }

    /// <summary>The client every capability uses for protected calls (<c>EX-004</c>).</summary>
    public HttpClient CreateApiClient() =>
        Services.GetRequiredService<IHttpClientFactory>().CreateClient(KohaClientNames.Api);

    public void Dispose() => Services.Dispose();
}
