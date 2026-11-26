using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minioasis.Api.Authentication.Providers;
using Minioasis.Api.Tests.Shared.TestDoubles;

namespace Minioasis.Api.Tests.Shared;

/// <summary>
/// The one way Api tests start the application (TS-009): dummy Koha and Cloudflare Access settings, Cloudflare's key
/// address answered by <see cref="KeySet"/>, settings and service overrides for this host only, the environment name,
/// and a log the test can read.
/// </summary>
/// <param name="fetchRealKeys">
/// True only for a test Koha check: the key fetch then goes to the real address that the settings name.
/// </param>
internal sealed class ApiTestHostFactory(
    Action<IServiceCollection>? configureServices = null,
    IReadOnlyDictionary<string, string?>? settings = null,
    string environment = "Production",
    bool fetchRealKeys = false) : WebApplicationFactory<Program>
{
    public const string RequestedWithHeader = "X-Requested-With";
    public const string RequestedWithValue = "XMLHttpRequest";
    public const string TokenHeader = "Cf-Access-Jwt-Assertion";

    private static readonly Dictionary<string, string?> DummySettings = new()
    {
        ["MINIOASIS_KOHA_BASE_URL"] = "https://koha.test",
        ["MINIOASIS_KOHA_CLIENT_ID"] = "test-client",
        ["MINIOASIS_KOHA_CLIENT_SECRET"] = "test-secret",
        ["MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN"] = StaffTokens.TeamDomain,
        ["MINIOASIS_CLOUDFLARE_ACCESS_AUDIENCE"] = StaffTokens.Audience,
        ["MINIOASIS_CLOUDFLARE_ACCESS_EMAIL"] = StaffTokens.Email,
    };

    public CapturingLoggerProvider Logs { get; } = new();

    /// <summary>Cloudflare's key address, as this host sees it.</summary>
    public KeySetHandler KeySet { get; } = new();

    /// <summary>
    /// A client that sends what the Minioasis frontend sends behind Cloudflare Access: a valid token for the
    /// configured email (or <paramref name="token"/>), and <c>X-Requested-With: XMLHttpRequest</c>.
    /// </summary>
    public HttpClient CreateStaffClient(string? token = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TokenHeader, token ?? StaffTokens.Create());
        client.DefaultRequestHeaders.Add(RequestedWithHeader, RequestedWithValue);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var merged = new Dictionary<string, string?>(DummySettings);
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            merged[key] = value;
        }

        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(merged));
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(Logs));
        builder.ConfigureTestServices(services =>
        {
            if (!fetchRealKeys)
            {
                services.AddHttpClient(CloudflareAccessKeySetProvider.ClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => KeySet);
            }

            configureServices?.Invoke(services);
        });
    }
}
