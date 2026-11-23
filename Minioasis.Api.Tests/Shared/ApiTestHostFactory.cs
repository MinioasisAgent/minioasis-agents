using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minioasis.Api.Tests.Shared.TestDoubles;

namespace Minioasis.Api.Tests.Shared;

/// <summary>
/// The one way Api tests start the application (TS-009): dummy Koha settings, settings and service overrides for
/// this host only, the environment name, and a log the test can read.
/// </summary>
internal sealed class ApiTestHostFactory(
    Action<IServiceCollection>? configureServices = null,
    IReadOnlyDictionary<string, string?>? settings = null,
    string environment = "Production") : WebApplicationFactory<Program>
{
    private static readonly Dictionary<string, string?> DummyKohaSettings = new()
    {
        ["MINIOASIS_KOHA_BASE_URL"] = "https://koha.test",
        ["MINIOASIS_KOHA_CLIENT_ID"] = "test-client",
        ["MINIOASIS_KOHA_CLIENT_SECRET"] = "test-secret",
    };

    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var merged = new Dictionary<string, string?>(DummyKohaSettings);
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            merged[key] = value;
        }

        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(merged));
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(Logs));
        builder.ConfigureTestServices(services => configureServices?.Invoke(services));
    }
}
