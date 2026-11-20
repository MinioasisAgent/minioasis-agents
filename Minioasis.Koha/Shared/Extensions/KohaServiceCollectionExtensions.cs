using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Minioasis.Koha.Authentication.Handlers;
using Minioasis.Koha.Authentication.Providers;
using Minioasis.Koha.Shared.Clients;
using Minioasis.Koha.Shared.Exceptions;
using Minioasis.Koha.Shared.Factories;
using Minioasis.Koha.Shared.Handlers;

namespace Minioasis.Koha.Shared.Extensions;

public static class KohaServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Koha connection: the checked options; the token provider, with <see cref="TimeProvider.System"/>
    /// unless a time provider is already registered; and the two named clients (<c>EX-007</c>) with their handlers,
    /// redacted header logging, and no automatic redirects. The options are read and checked now, while registering,
    /// so that an invalid variable stops the application before it starts (spec 001, R10 to R13). Nothing here
    /// contacts Koha (D6, R14).
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

        // One instance: one token cache and one shared token request for the whole application (R17, R18).
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<KohaAccessTokenProvider>();
        services.AddTransient<KohaDeadlineHandler>();
        services.AddTransient<KohaBearerHandler>();

        // EX-007: the token request never passes through the bearer handler. The deadline handler bounds the shared
        // token request on its own (R18).
        AddKohaClient(services, KohaClientNames.OAuth)
            .AddHttpMessageHandler<KohaDeadlineHandler>();

        // The deadline first, then the bearer handler, so that one deadline covers the token step, the call, the
        // retry, and the body (R21).
        AddKohaClient(services, KohaClientNames.Api)
            .AddHttpMessageHandler<KohaDeadlineHandler>()
            .AddHttpMessageHandler<KohaBearerHandler>();

        return services;
    }

    private static IHttpClientBuilder AddKohaClient(IServiceCollection services, string name) =>
        services
            // HttpClient's own timeout is switched off: the Koha deadline (R21) is applied in one place, by a handler
            // (CD-006, EX-011), not by HttpClient.
            .AddHttpClient(name, client => client.Timeout = Timeout.InfiniteTimeSpan)
            // No automatic redirects: a 3xx is a final reply (R20), and no request, the token request with the
            // secret included, is ever sent again to another address (R15, R19, EX-010).
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
            // R23, ER-008: no header value is ever logged, whatever the application's default for HttpClient logging.
            .RedactLoggedHeaders(_ => true);
}
