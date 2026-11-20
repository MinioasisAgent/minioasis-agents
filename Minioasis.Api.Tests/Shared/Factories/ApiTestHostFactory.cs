using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minioasis.Api.Shared.Abstractions;
using Minioasis.Api.Tests.Shared.TestDoubles;

namespace Minioasis.Api.Tests.Shared.Factories;

/// <summary>
/// The one Api test host (TS-009). Each instance has its own settings, environment, log capture, test endpoints,
/// and service overrides. It supplies dummy Koha settings, so that the host starts without a Koha (TS-008).
/// </summary>
/// <param name="configureServices">Overrides applied after the application's own registrations.</param>
/// <param name="settings">
/// Settings for this host only, applied with <c>UseSetting</c> after the dummy Koha settings, so they replace them and
/// are visible before <c>Program.cs</c> reads them.
/// </param>
/// <param name="environment">The host environment name.</param>
/// <param name="logging">Whether any logger provider is registered; when false, nothing is logged anywhere.</param>
public sealed class ApiTestHostFactory(
    Action<IServiceCollection>? configureServices = null,
    IReadOnlyDictionary<string, string?>? settings = null,
    string environment = "Production",
    bool logging = true) : WebApplicationFactory<Program>
{
    // Dummy Koha settings (TS-008): the host starts, and nothing reaches a Koha (spec 001, R14).
    private static readonly Dictionary<string, string?> DummyKohaSettings = new()
    {
        ["MINIOASIS_KOHA_BASE_URL"] = "https://koha.test/api/v1",
        ["MINIOASIS_KOHA_CLIENT_ID"] = "dummy-client-id",
        ["MINIOASIS_KOHA_CLIENT_SECRET"] = "dummy-client-secret",
    };

    public RecordingLoggerProvider Logs { get; } = new();

    public TestEndpointModule TestEndpoints { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        foreach (var (key, value) in DummyKohaSettings.Concat(settings ?? new Dictionary<string, string?>()))
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            if (logging)
            {
                loggingBuilder.SetMinimumLevel(LogLevel.Trace);
                loggingBuilder.AddProvider(Logs);
            }
        });
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IEndpointModule>(TestEndpoints);
            services.AddTransient<IStartupFilter, OversizedBodyStartupFilter>();
            configureServices?.Invoke(services);
        });
    }
}
