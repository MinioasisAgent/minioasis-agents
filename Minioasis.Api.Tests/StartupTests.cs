using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Minioasis.Api.Tests.Shared;
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
}
