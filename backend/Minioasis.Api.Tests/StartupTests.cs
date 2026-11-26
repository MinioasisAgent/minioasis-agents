using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Minioasis.Api.Tests.Shared;
using Minioasis.Api.Tests.Shared.TestDoubles;
using Minioasis.Application.Shared.Errors;
using Xunit;

namespace Minioasis.Api.Tests;

public sealed class StartupTests
{
    [Fact]
    public async Task Starts_with_valid_settings()
    {
        await using var factory = new ApiTestHostFactory();

        Assert.NotNull(factory.Server);
    }

    [Theory]
    [InlineData("MINIOASIS_KOHA_BASE_URL", null)]
    [InlineData("MINIOASIS_KOHA_BASE_URL", "http://koha.internal")]
    [InlineData("MINIOASIS_KOHA_CLIENT_ID", " ")]
    [InlineData("MINIOASIS_KOHA_CLIENT_SECRET", "")]
    public async Task Refuses_to_start_with_a_bad_koha_setting_and_names_it_without_its_value(string setting, string? value)
    {
        await using var factory = new ApiTestHostFactory(settings: new Dictionary<string, string?> { [setting] = value });

        var exception = Assert.ThrowsAny<Exception>(() => factory.Server);

        // The host logs the cause before it stops. Under load, the test factory sometimes throws an
        // ObjectDisposedException in place of that cause, so the test reads the cause from the log.
        var cause = Assert.Single(factory.Logs.Entries, entry => entry.Exception is OptionsValidationException).Exception!;
        Assert.Contains(setting, cause.Message, StringComparison.Ordinal);
        foreach (var text in new[] { cause.ToString(), exception.ToString() })
        {
            Assert.DoesNotContain("koha.internal", text, StringComparison.Ordinal);
            Assert.DoesNotContain("test-secret", text, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN", null)]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN", "")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN", "  ")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN", "example.cloudflareaccess.com")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN", "/cdn-cgi/access/certs")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN", "http://example.cloudflareaccess.com")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN", "http://localhost:8082")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN", "ftp://example.cloudflareaccess.com")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN", "https://example.cloudflareaccess.com/cdn-cgi")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN", "https://example.cloudflareaccess.com?team=a")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN", "https://example.cloudflareaccess.com/?")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN", "https://example.cloudflareaccess.com#keys")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN", "https://user@example.cloudflareaccess.com")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN", "https://user:password@example.cloudflareaccess.com")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_AUDIENCE", null)]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_AUDIENCE", "")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_AUDIENCE", " ")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_EMAIL", null)]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_EMAIL", "")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_EMAIL", "\t")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_EMAIL", "staff")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_EMAIL", "@example.org")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_EMAIL", "staff@")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_EMAIL", "a b@example.org")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_EMAIL", "staff@@example.org")]
    [InlineData("MINIOASIS_CLOUDFLARE_ACCESS_EMAIL", "staff@example@org")]
    public async Task Behavior_002_14_refuses_to_start_with_a_bad_cloudflare_access_setting_and_names_it_without_its_value(
        string setting,
        string? value)
    {
        await using var factory = new ApiTestHostFactory(settings: new Dictionary<string, string?> { [setting] = value });

        var exception = Assert.ThrowsAny<Exception>(() => factory.Server);

        var cause = Assert.Single(factory.Logs.Entries, entry => entry.Exception is OptionsValidationException).Exception!;
        Assert.Contains(setting, cause.Message, StringComparison.Ordinal);
        var otherSettings = new[] { "MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN", "MINIOASIS_CLOUDFLARE_ACCESS_AUDIENCE", "MINIOASIS_CLOUDFLARE_ACCESS_EMAIL" }
            .Where(name => name != setting);
        Assert.All(otherSettings, name => Assert.DoesNotContain(name, cause.Message, StringComparison.Ordinal));
        string[] values = string.IsNullOrWhiteSpace(value)
            ? [StaffTokens.Audience, StaffTokens.Email, "example.cloudflareaccess.com"]
            : [value, StaffTokens.Audience, StaffTokens.Email];
        foreach (var text in new[] { cause.ToString(), exception.ToString() })
        {
            Assert.All(values, secret => Assert.DoesNotContain(secret, text, StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("https://example.cloudflareaccess.com")]
    [InlineData("https://example.cloudflareaccess.com/")]
    [InlineData("https://localhost:8443")]
    public async Task Behavior_002_14_starts_with_a_valid_team_domain(string teamDomain)
    {
        await using var factory = new ApiTestHostFactory(
            settings: new Dictionary<string, string?> { ["MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN"] = teamDomain });

        Assert.NotNull(factory.Server);
    }

    [Fact]
    public async Task Refuses_to_start_when_the_error_registry_and_the_message_file_disagree()
    {
        await using var factory = new ApiTestHostFactory(services =>
        {
            services.RemoveAll<ApplicationErrorRegistry>();
            services.AddSingleton(new ApplicationErrorRegistry(
            [
                .. ApplicationErrorRegistry.Default.Definitions,
                new ApplicationErrorDefinition("things.not_found", ApplicationErrorCategory.NotFound),
            ]));
        });

        var exception = Assert.ThrowsAny<Exception>(() => factory.Server);

        Assert.Contains("things.not_found", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_to_start_when_application_defines_a_code_that_api_defines_as_an_error_of_http()
    {
        await using var factory = new ApiTestHostFactory(services =>
        {
            services.RemoveAll<ApplicationErrorRegistry>();
            services.AddSingleton(new ApplicationErrorRegistry(
            [
                .. ApplicationErrorRegistry.Default.Definitions,
                new ApplicationErrorDefinition("method_not_allowed", ApplicationErrorCategory.Validation),
            ]));
        });

        var exception = Assert.ThrowsAny<Exception>(() => factory.Server);

        Assert.Contains("method_not_allowed", exception.ToString(), StringComparison.Ordinal);
    }
}
