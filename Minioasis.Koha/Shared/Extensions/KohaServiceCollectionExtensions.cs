using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minioasis.Koha.Shared.Clients;
using Minioasis.Koha.Shared.Exceptions;
using Minioasis.Koha.Shared.Factories;

namespace Minioasis.Koha.Shared.Extensions;

public static class KohaServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Koha connection: the checked options and the two named clients (<c>EX-007</c>). The options are
    /// read and checked now, while registering, so that an invalid variable stops the application before it starts
    /// (spec 001, R10 to R13). Nothing here contacts Koha (D6, R14).
    /// </summary>
    /// <param name="allowInsecureHttp">
    /// True only in the <c>Development</c> environment, where plain http is allowed (D5).
    /// </param>
    /// <exception cref="KohaConfigurationException">A Koha variable is missing, blank, or invalid.</exception>
    public static IServiceCollection AddKohaIntegration(
        this IServiceCollection services,
        IConfiguration configuration,
        bool allowInsecureHttp)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(KohaOptionsFactory.Create(configuration, allowInsecureHttp));

        // HttpClient's own timeout is switched off: the Koha deadline (R21) is applied in one place, by a handler
        // (CD-006, EX-011), not by HttpClient.
        services.AddHttpClient(KohaClientNames.OAuth, client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddHttpClient(KohaClientNames.Api, client => client.Timeout = Timeout.InfiniteTimeSpan);

        return services;
    }
}
