using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Minioasis.Api.Tests.Shared;
using Xunit;

namespace Minioasis.Api.Tests.Authentication;

/// <summary>
/// The wiring of staff access control in the unmodified host (TS-005), and the settings of brief 002, part 7, that
/// differ from the library's defaults.
/// </summary>
public sealed class StaffAccessExtensionsTests
{
    [Fact]
    public async Task The_unmodified_host_uses_one_configuration_manager_for_the_handler_and_the_check_of_the_keys()
    {
        await using var factory = new ApiTestHostFactory();
        _ = factory.Server;

        var keys = factory.Services.GetRequiredService<IConfigurationManager<OpenIdConnectConfiguration>>();
        var jwt = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("CloudflareAccess");

        var manager = Assert.IsType<ConfigurationManager<OpenIdConnectConfiguration>>(keys);
        Assert.Same(manager, jwt.ConfigurationManager);
        Assert.Equal("https://example.cloudflareaccess.com/cdn-cgi/access/certs", manager.MetadataAddress);
        Assert.False(manager.UseLastKnownGoodConfiguration);
    }

    [Fact]
    public async Task The_unmodified_host_registers_no_data_protection()
    {
        await using var factory = new ApiTestHostFactory();
        _ = factory.Server;

        // Nothing in Minioasis protects data. With Data Protection registered, every host start loads a key ring, and
        // on a machine without one it writes a key to the user profile and logs a warning.
        Assert.Null(factory.Services.GetService<IDataProtectionProvider>());
        Assert.Null(factory.Services.GetService<IKeyManager>());
    }

    [Fact]
    public async Task The_token_handler_takes_no_settings_from_the_authentication_configuration_section()
    {
        await using var factory = new ApiTestHostFactory(settings: new Dictionary<string, string?>
        {
            ["Authentication:Schemes:CloudflareAccess:ForwardAuthenticate"] = "Other",
            ["Authentication:Schemes:CloudflareAccess:RefreshOnIssuerKeyNotFound"] = "false",
        });
        _ = factory.Server;

        var jwt = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("CloudflareAccess");

        Assert.Null(jwt.ForwardAuthenticate);
        Assert.True(jwt.RefreshOnIssuerKeyNotFound);
    }

    [Fact]
    public async Task The_token_handler_has_the_settings_of_the_brief()
    {
        await using var factory = new ApiTestHostFactory();
        _ = factory.Server;

        var jwt = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("CloudflareAccess");
        var parameters = jwt.TokenValidationParameters;

        Assert.True(AppContext.TryGetSwitch("Switch.Microsoft.IdentityModel.UpdateConfigAsBlocking", out var blocking) && blocking);
        Assert.Equal("CloudflareAccess", jwt.Challenge);
        Assert.False(jwt.IncludeErrorDetails);
        Assert.False(jwt.MapInboundClaims);
        Assert.False(jwt.SaveToken);
        Assert.Equal(["RS256"], parameters.ValidAlgorithms);
        Assert.Equal(TimeSpan.FromSeconds(60), parameters.ClockSkew);
        Assert.False(parameters.IgnoreTrailingSlashWhenValidatingAudience);
        Assert.False(parameters.ValidateWithLKG);
        Assert.True(parameters.RequireExpirationTime);
        Assert.True(parameters.RequireSignedTokens);
        Assert.Equal("https://example.cloudflareaccess.com", parameters.ValidIssuer);
        Assert.Equal("test-audience-tag", parameters.ValidAudience);
    }
}
