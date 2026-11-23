using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Minioasis.Application.Items.Abstractions;
using Minioasis.Koha.Authentication.Handlers;
using Minioasis.Koha.Authentication.Providers;
using Minioasis.Koha.Items.Adapters;
using Minioasis.Koha.Shared.Handlers;
using Minioasis.Koha.Shared.Http;
using Minioasis.Koha.Shared.Options;
using System.Net.Http.Headers;
using System.Text;

namespace Minioasis.Koha;

public static class KohaServiceCollectionExtensions
{
    /// <summary>
    /// Registers the connection to Koha and the gateways that use it (AR-005). The settings are read from
    /// <paramref name="configuration"/> when the options are first needed, and checked when the host starts.
    /// </summary>
    public static IServiceCollection AddKohaIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<KohaOptions>()
            .Configure(options =>
            {
                options.BaseUrl = configuration[KohaOptions.BaseUrlSetting];
                options.ClientId = configuration[KohaOptions.ClientIdSetting];
                options.ClientSecret = configuration[KohaOptions.ClientSecretSetting];
            })
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<KohaOptions>, KohaOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<KohaAccessTokenProvider>();
        services.AddTransient<KohaDeadlineHandler>();
        services.AddTransient<KohaBearerHandler>();
        services.AddTransient<KohaCharsetCorrectionHandler>();

        services.AddHttpClient(KohaClientNames.OAuth)
            .ConfigureHttpClient((provider, client) =>
                client.DefaultRequestHeaders.Authorization = BasicCredentials(
                    provider.GetRequiredService<IOptions<KohaOptions>>().Value))
            .ConfigurePrimaryHttpMessageHandler(CreatePrimaryHandler)
            .AddHttpMessageHandler<KohaCharsetCorrectionHandler>();

        services.AddHttpClient(KohaClientNames.Api)
            .ConfigurePrimaryHttpMessageHandler(CreatePrimaryHandler)
            .AddHttpMessageHandler<KohaDeadlineHandler>()
            .AddHttpMessageHandler<KohaBearerHandler>()
            .AddHttpMessageHandler<KohaCharsetCorrectionHandler>();

        services.AddScoped<IGetItemGateway, KohaGetItemAdapter>();
        return services;
    }

    /// <summary>
    /// Redirects are not followed: a redirect means that the base URL is wrong, and following it would hide that.
    /// </summary>
    private static SocketsHttpHandler CreatePrimaryHandler() => new() { AllowAutoRedirect = false };

    /// <summary>
    /// Base64 of <c>identifier:secret</c> in UTF-8, as curl builds it (brief 001, part 4).
    /// </summary>
    private static AuthenticationHeaderValue BasicCredentials(KohaOptions options) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.ClientId}:{options.ClientSecret}")));
}
