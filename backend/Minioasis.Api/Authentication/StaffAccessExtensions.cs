using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Minioasis.Api.Authentication.Handlers;
using Minioasis.Api.Authentication.Options;
using Minioasis.Api.Authentication.Providers;

namespace Minioasis.Api.Authentication;

/// <summary>
/// Staff access control (brief 002): ASP.NET's JWT bearer authentication validates the Cloudflare Access token with
/// the settings of part 7, and <see cref="StaffAccessHandler"/> applies the checks in front of routing.
/// </summary>
internal static class StaffAccessExtensions
{
    public const string AuthenticationScheme = "CloudflareAccess";

    /// <summary>The only token header. There is no fallback to <c>Authorization</c> or to the cookie.</summary>
    private const string TokenHeader = "Cf-Access-Jwt-Assertion";

    /// <summary>The value of <c>WWW-Authenticate</c> on a 401: a scheme of our own, without parameters.</summary>
    private const string Challenge = "CloudflareAccess";

    /// <summary>
    /// The library reads this switch once. With it, a request that needs a fetch of the keys waits for it, so the
    /// first request after Cloudflare changes its key is accepted. It must be set before anything else runs.
    /// </summary>
    public const string BlockingRefreshSwitch = "Switch.Microsoft.IdentityModel.UpdateConfigAsBlocking";

    private const string TypeClaim = "type";
    private const string ApplicationTokenType = "app";

    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Registers the settings and their startup check, the key fetch, and the token handler (AR-005).
    /// </summary>
    public static IServiceCollection AddStaffAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CloudflareAccessOptions>()
            .Configure(options =>
            {
                options.TeamDomain = configuration[CloudflareAccessOptions.TeamDomainSetting];
                options.Audience = configuration[CloudflareAccessOptions.AudienceSetting];
                options.Email = configuration[CloudflareAccessOptions.EmailSetting];
            })
            .ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<CloudflareAccessOptions>, CloudflareAccessOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<CloudflareAccessKeySetProvider>();
        services.AddHttpClient(CloudflareAccessKeySetProvider.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(CreateKeyManager);

        AddAuthenticationWithoutDataProtection(services).AddJwtBearer(AuthenticationScheme);
        services.AddOptions<JwtBearerOptions>(AuthenticationScheme)
            .Configure<IOptions<CloudflareAccessOptions>, IConfigurationManager<OpenIdConnectConfiguration>>(ConfigureTokenHandler);
        return services;
    }

    /// <summary>Adds the checks of staff access control. Call it before routing.</summary>
    /// <remarks>
    /// When nothing adds the authentication middleware, the framework adds it at the very start of the pipeline, where
    /// it would validate the token before the keys are checked. Adding it here, after the checks, keeps the order of
    /// part 3. It reuses the result that the checks obtained, and only sets <see cref="HttpContext.User"/>.
    /// </remarks>
    public static IApplicationBuilder UseStaffAccess(this IApplicationBuilder app) =>
        app.UseMiddleware<StaffAccessHandler>().UseAuthentication();

    /// <summary>
    /// What <c>AddAuthentication()</c> registers, less Data Protection, which nothing here uses: the JWT bearer handler
    /// protects no data. With it, every host start loads a key ring, and on a machine without one writes a key to the
    /// user profile and logs a warning. The handler's settings come only from <see cref="ConfigureTokenHandler"/>:
    /// the configuration section <c>Authentication</c> is not read, so no setting but the three of brief 002 changes
    /// how a token is validated.
    /// </summary>
    private static AuthenticationBuilder AddAuthenticationWithoutDataProtection(IServiceCollection services)
    {
        services.AddAuthenticationCore();
        services.AddWebEncoders();
        services.TryAddSingleton<IAuthenticationConfigurationProvider, NoAuthenticationConfiguration>();
        return new AuthenticationBuilder(services);
    }

    /// <summary>
    /// The library's configuration manager over Cloudflare's key address. "Last known good" is off, so a key that the
    /// newest fetch no longer holds is refused at once.
    /// </summary>
    private static ConfigurationManager<OpenIdConnectConfiguration> CreateKeyManager(IServiceProvider provider)
    {
        var keySet = provider.GetRequiredService<CloudflareAccessKeySetProvider>();
        return new ConfigurationManager<OpenIdConnectConfiguration>(
            provider.GetRequiredService<IOptions<CloudflareAccessOptions>>().Value.KeySetAddress,
            keySet,
            keySet)
        {
            UseLastKnownGoodConfiguration = false,
        };
    }

    private static void ConfigureTokenHandler(
        JwtBearerOptions jwt,
        IOptions<CloudflareAccessOptions> access,
        IConfigurationManager<OpenIdConnectConfiguration> signingKeys)
    {
        jwt.ConfigurationManager = signingKeys;
        jwt.Challenge = Challenge;
        jwt.IncludeErrorDetails = false;
        jwt.MapInboundClaims = false;
        jwt.SaveToken = false;
        jwt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = access.Value.Issuer,
            ValidAudience = access.Value.Audience,
            IgnoreTrailingSlashWhenValidatingAudience = false,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ClockSkew = ClockSkew,
            ValidateWithLKG = false,
        };
        jwt.Events = new JwtBearerEvents
        {
            OnMessageReceived = ReadTokenHeader,
            OnTokenValidated = RequireApplicationToken,
        };
    }

    /// <summary>
    /// Reads the token from <see cref="TokenHeader"/> only. Without exactly one value there, the result is "no token",
    /// so that the handler does not look in <c>Authorization</c> instead.
    /// </summary>
    private static Task ReadTokenHeader(MessageReceivedContext context)
    {
        var values = context.Request.Headers[TokenHeader];
        if (values.Count == 1 && !string.IsNullOrEmpty(values[0]))
        {
            context.Token = values[0];
        }
        else
        {
            context.NoResult();
        }

        return Task.CompletedTask;
    }

    /// <summary>Refuses every token but an application token, such as Cloudflare's global session token.</summary>
    private static Task RequireApplicationToken(TokenValidatedContext context)
    {
        var types = context.Principal?.FindAll(TypeClaim).ToList() ?? [];
        if (types.Count != 1 || !string.Equals(types[0].Value, ApplicationTokenType, StringComparison.Ordinal))
        {
            context.Fail("The token is not an application token.");
        }

        return Task.CompletedTask;
    }

    /// <summary>An empty authentication configuration, so that no scheme takes settings from configuration.</summary>
    private sealed class NoAuthenticationConfiguration : IAuthenticationConfigurationProvider
    {
        public IConfiguration AuthenticationConfiguration { get; } = new ConfigurationBuilder().Build();
    }
}
