using Minioasis.Api.Shared.Hosting;

namespace Minioasis.Api.Shared.Extensions;

public static class RequestActivityExtensions
{
    /// <summary>
    /// Registers the listener that gives every request an activity (R4), created and disposed with the host.
    /// </summary>
    public static IServiceCollection AddRequestActivityListener(this IServiceCollection services) =>
        services.AddHostedService<RequestActivityListenerHostedService>();
}
