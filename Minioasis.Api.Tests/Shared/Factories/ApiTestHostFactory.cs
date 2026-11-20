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

    private static readonly string[] KohaVariablePrefixes =
        ["MINIOASIS_KOHA_", "ASPNETCORE_MINIOASIS_KOHA_", "DOTNET_MINIOASIS_KOHA_"];

    public RecordingLoggerProvider Logs { get; } = new();

    public TestEndpointModule TestEndpoints { get; } = new();

    /// <summary>
    /// Refuses to build a test host when the process environment sets a Koha variable (TS-008, TS-009): such a value
    /// could reach the host's configuration, and with it a real Koha URL or secret. Only names are read, never values.
    /// </summary>
    /// <exception cref="InvalidOperationException">A Koha variable is set; the message names each one.</exception>
    public static void EnsureNoKohaVariables(IEnumerable<string> variableNames)
    {
        var kohaVariables = variableNames.Where(IsKohaVariable).Order(StringComparer.Ordinal).ToList();
        if (kohaVariables.Count > 0)
        {
            throw new InvalidOperationException(
                $"The process environment sets {string.Join(", ", kohaVariables)}. Run the tests in a shell without "
                + "any MINIOASIS_KOHA_* variable, so that no test host can carry real Koha settings.");
        }
    }

    // The configuration reads the variables without regard to case, and also with the ASPNETCORE_ and DOTNET_
    // prefixes that the host strips.
    private static bool IsKohaVariable(string name) =>
        KohaVariablePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        EnsureNoKohaVariables(Environment.GetEnvironmentVariables().Keys.Cast<string>());
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
