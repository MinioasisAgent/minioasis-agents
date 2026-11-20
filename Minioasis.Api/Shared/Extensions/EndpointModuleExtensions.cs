using Minioasis.Api.Shared.Abstractions;

namespace Minioasis.Api.Shared.Extensions;

public static class EndpointModuleExtensions
{
    /// <summary>Maps the endpoints of every <see cref="IEndpointModule"/> registered in dependency injection.</summary>
    public static IEndpointRouteBuilder MapEndpointModules(this IEndpointRouteBuilder endpoints)
    {
        foreach (var module in endpoints.ServiceProvider.GetServices<IEndpointModule>())
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
