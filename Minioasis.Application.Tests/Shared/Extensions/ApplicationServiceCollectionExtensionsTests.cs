using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Minioasis.Application.Shared.Errors;
using Minioasis.Application.Shared.Extensions;
using Xunit;

namespace Minioasis.Application.Tests.Shared.Extensions;

public sealed class ApplicationServiceCollectionExtensionsTests
{
    private static readonly ServiceProviderOptions Validating = new() { ValidateOnBuild = true, ValidateScopes = true };

    [Fact]
    public void Resolves_registry()
    {
        using var provider = new ServiceCollection().AddApplication().BuildServiceProvider(Validating);

        var registry = provider.GetRequiredService<ApplicationErrorDefinitionRegistry>();

        Assert.Equal(
            [
                ("koha.authentication_failed", ApplicationErrorCategory.DependencyAuthenticationFailed),
                ("koha.invalid_response", ApplicationErrorCategory.DependencyInvalidResponse),
                ("koha.unavailable", ApplicationErrorCategory.DependencyUnavailable),
                ("method_not_allowed", ApplicationErrorCategory.MethodNotAllowed),
                ("not_found", ApplicationErrorCategory.NotFound),
                ("request.invalid", ApplicationErrorCategory.Validation),
                ("system.unexpected", ApplicationErrorCategory.Unexpected),
            ],
            registry.Definitions
                .Select(definition => (definition.Code, definition.Category))
                .OrderBy(pair => pair.Code, StringComparer.Ordinal));
    }

    [Fact]
    public void Registry_is_a_single_instance()
    {
        using var provider = new ServiceCollection().AddApplication().BuildServiceProvider(Validating);

        Assert.Same(
            provider.GetRequiredService<ApplicationErrorDefinitionRegistry>(),
            provider.GetRequiredService<ApplicationErrorDefinitionRegistry>());
    }

    [Fact]
    public void Registry_checks_the_registered_source()
    {
        var replacement = new ApplicationErrorDefinition("test.replacement", ApplicationErrorCategory.Validation);
        var services = new ServiceCollection().AddApplication();
        services.RemoveAll<ApplicationErrorDefinitionSource>();
        services.AddSingleton(new ApplicationErrorDefinitionSource([replacement]));
        using var provider = services.BuildServiceProvider(Validating);

        var registry = provider.GetRequiredService<ApplicationErrorDefinitionRegistry>();

        Assert.Equal([replacement], registry.Definitions);
    }

    [Fact]
    public void Inconsistent_registered_source_fails_when_the_registry_is_resolved()
    {
        var services = new ServiceCollection().AddApplication();
        services.RemoveAll<ApplicationErrorDefinitionSource>();
        services.AddSingleton(new ApplicationErrorDefinitionSource([]));
        using var provider = services.BuildServiceProvider(Validating);

        var exception = Assert.Throws<InvalidOperationException>(
            provider.GetRequiredService<ApplicationErrorDefinitionRegistry>);

        Assert.Contains("empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Null_services_throw()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => ApplicationServiceCollectionExtensions.AddApplication(null!));

        Assert.Equal("services", exception.ParamName);
    }
}
