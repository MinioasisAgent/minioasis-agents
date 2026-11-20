using Microsoft.Extensions.DependencyInjection;
using Minioasis.Application.Shared.Errors;

namespace Minioasis.Application.Shared.Extensions;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Application services: the error definitions of the Application assembly, and the registry
    /// that checks them when it is first resolved.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(_ => ApplicationErrorDefinitionSource.FromAssembly(
            typeof(ApplicationServiceCollectionExtensions).Assembly));
        services.AddSingleton<ApplicationErrorDefinitionRegistry>();

        return services;
    }
}
