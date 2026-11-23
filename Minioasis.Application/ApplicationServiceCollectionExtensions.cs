using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Minioasis.Application.Items.UseCases;
using Minioasis.Application.Shared.Errors;

namespace Minioasis.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the use cases and the error registry (AR-005).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(ApplicationErrorRegistry.Default);
        services.AddScoped<IGetItemUseCase, GetItemUseCase>();
        return services;
    }
}
