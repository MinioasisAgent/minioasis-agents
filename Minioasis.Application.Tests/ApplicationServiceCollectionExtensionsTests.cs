using Microsoft.Extensions.DependencyInjection;
using Minioasis.Application.Items.UseCases;
using Minioasis.Application.Shared.Errors;
using Xunit;

namespace Minioasis.Application.Tests;

/// <summary>
/// Application references only the abstractions of dependency injection, so these tests read the registrations.
/// Resolving them from the real host is tested in Minioasis.Api.Tests.
/// </summary>
public sealed class ApplicationServiceCollectionExtensionsTests
{
    [Fact]
    public void Registers_the_get_item_use_case_per_request()
    {
        var services = new ServiceCollection().AddApplication();

        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(IGetItemUseCase));
        Assert.Equal(typeof(GetItemUseCase), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void Registers_the_default_error_registry_once()
    {
        var services = new ServiceCollection().AddApplication();

        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(ApplicationErrorRegistry));
        Assert.Same(ApplicationErrorRegistry.Default, descriptor.ImplementationInstance);
    }
}
