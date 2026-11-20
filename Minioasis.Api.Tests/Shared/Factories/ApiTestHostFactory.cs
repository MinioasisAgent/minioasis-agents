using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minioasis.Api.Shared.Abstractions;
using Minioasis.Api.Tests.Shared.TestDoubles;

namespace Minioasis.Api.Tests.Shared.Factories;

/// <summary>
/// The one Api test host (TS-009). Each instance has its own settings, environment, log capture, test endpoints,
/// and service overrides.
/// </summary>
/// <param name="configureServices">Overrides applied after the application's own registrations.</param>
/// <param name="settings">Settings for this host only, applied with <c>UseSetting</c>.</param>
/// <param name="environment">The host environment name.</param>
/// <param name="logging">Whether any logger provider is registered; when false, nothing is logged anywhere.</param>
public sealed class ApiTestHostFactory(
    Action<IServiceCollection>? configureServices = null,
    IReadOnlyDictionary<string, string?>? settings = null,
    string environment = "Production",
    bool logging = true) : WebApplicationFactory<Program>
{
    public RecordingLoggerProvider Logs { get; } = new();

    public TestEndpointModule TestEndpoints { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
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
