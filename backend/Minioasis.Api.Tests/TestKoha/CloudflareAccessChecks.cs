using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Minioasis.Api.Tests.Authentication.TestDoubles;
using Minioasis.Api.Tests.Items.TestDoubles;
using Minioasis.Api.Tests.Shared;
using Minioasis.Api.Tests.Shared.TestDoubles;
using Minioasis.Application.Items.Abstractions;
using Minioasis.Application.Items.Responses;
using Minioasis.Application.Items.Results;
using System.Buffers.Text;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Minioasis.Api.Tests.TestKoha;

/// <summary>
/// Brief 002, behavior 16, against Cloudflare itself (TS-013): a token that Cloudflare Access issued for the test
/// Koha's application passes every check of the token against the keys fetched from Cloudflare. The check gets the
/// token from the test Koha's address with the service token, and reads the team domain and the audience from it.
/// It skips itself when the five MINIOASIS_TEST_KOHA_* variables are absent. No value it reads is printed.
/// </summary>
[Trait("Category", "TestKoha")]
public sealed class CloudflareAccessChecks
{
    private const string SkipReason =
        "test Koha: not run, because one of the five MINIOASIS_TEST_KOHA_* variables is not set.";

    private static readonly TimeSpan CheckLimit = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Behavior_16_a_token_that_cloudflare_access_issued_passes_every_check_of_the_token()
    {
        var token = await GetCloudflareTokenAsync();
        var (teamDomain, audience) = ReadIssuerAndAudience(token);
        var gateway = new FakeGetItemGateway(GetItemGatewayResult.Found(new GetItemResponse("12", "7", null, null)));
        await using var factory = new ApiTestHostFactory(
            services =>
            {
                services.RemoveAll<IGetItemGateway>();
                services.AddSingleton<IGetItemGateway>(gateway);
            },
            new Dictionary<string, string?>
            {
                ["MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN"] = teamDomain,
                ["MINIOASIS_CLOUDFLARE_ACCESS_AUDIENCE"] = audience,
            },
            fetchRealKeys: true);

        using var issued = await AccessRequest.SendAsync(factory, token).WaitAsync(CheckLimit, TestContext.Current.CancellationToken);
        using var changed = await AccessRequest.SendAsync(factory, ChangeOneCharacterOfTheSignature(token))
            .WaitAsync(CheckLimit, TestContext.Current.CancellationToken);

        // A service token has no email: every check of the token passed, and the email check refused it.
        Assert.Equal(HttpStatusCode.Forbidden, issued.StatusCode);
        using var body = JsonDocument.Parse(await issued.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("access_denied", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, changed.StatusCode);
        Assert.Equal(0, gateway.Calls);
    }

    /// <summary>
    /// Asks the test Koha's address with the service token of TS-013. Cloudflare Access answers with its application
    /// token in the cookie <c>CF_Authorization</c>.
    /// </summary>
    private static async Task<string> GetCloudflareTokenAsync()
    {
        var baseUrl = Environment.GetEnvironmentVariable("MINIOASIS_TEST_KOHA_BASE_URL");
        var clientId = Environment.GetEnvironmentVariable("MINIOASIS_TEST_KOHA_CF_ACCESS_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("MINIOASIS_TEST_KOHA_CF_ACCESS_CLIENT_SECRET");
        Assert.SkipWhen(
            new[] { "BASE_URL", "CLIENT_ID", "CLIENT_SECRET", "CF_ACCESS_CLIENT_ID", "CF_ACCESS_CLIENT_SECRET" }
                .Any(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable($"MINIOASIS_TEST_KOHA_{name}"))),
            SkipReason);

        using var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(baseUrl!), "/"));
        request.Headers.Add("CF-Access-Client-Id", clientId);
        request.Headers.Add("CF-Access-Client-Secret", clientSecret);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken)
            .WaitAsync(CheckLimit, TestContext.Current.CancellationToken);

        var cookie = response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.Select(value => value.Split(';')[0]).FirstOrDefault(value => value.StartsWith("CF_Authorization=", StringComparison.Ordinal))
            : null;
        Assert.True(cookie is not null, "Cloudflare Access returned no CF_Authorization cookie to the service token.");
        return cookie["CF_Authorization=".Length..];
    }

    /// <summary>
    /// The team domain is the token's issuer, and the audience its first audience. The payload is read without a
    /// check: the application then checks it.
    /// </summary>
    private static (string TeamDomain, string Audience) ReadIssuerAndAudience(string token)
    {
        using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(StaffTokens.PayloadOf(token)));
        var audience = payload.RootElement.GetProperty("aud");
        return (
            payload.RootElement.GetProperty("iss").GetString()!,
            audience.ValueKind == JsonValueKind.Array ? audience[0].GetString()! : audience.GetString()!);
    }

    /// <summary>
    /// Changes one character in the middle of the signature, where every character carries six bits of the
    /// signature, so that the signature no longer matches.
    /// </summary>
    private static string ChangeOneCharacterOfTheSignature(string token)
    {
        var signatureStart = token.LastIndexOf('.') + 1;
        var position = signatureStart + ((token.Length - signatureStart) / 2);
        var replacement = token[position] == 'A' ? 'B' : 'A';
        return string.Concat(token.AsSpan(0, position), replacement.ToString(), token.AsSpan(position + 1));
    }
}
