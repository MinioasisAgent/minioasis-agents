using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Minioasis.Api.Tests.Authentication.TestDoubles;
using Minioasis.Api.Tests.Items.TestDoubles;
using Minioasis.Api.Tests.Shared;
using Minioasis.Api.Tests.Shared.TestDoubles;
using Minioasis.Application.Items.Abstractions;
using Minioasis.Application.Items.Responses;
using Minioasis.Application.Items.Results;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Minioasis.Api.Tests.Authentication.Handlers;

/// <summary>
/// Brief 002, behaviors 1 to 9, 15, 17, and 18: the checks on the token, the email, and <c>X-Requested-With</c>, in
/// front of routing, against the host as it is really configured, with only Koha and Cloudflare's key address
/// replaced.
/// </summary>
public sealed class StaffAccessHandlerTests
{
    private const string UnauthenticatedTitle = "Sign-in required";
    private const string UnauthenticatedDetail = "Your sign-in could not be confirmed.";

    /// <summary>
    /// The tokens of behavior 3, by name. Each test builds its token when it runs: the theory data is read in another
    /// process, where the in-memory signing key is a different one.
    /// </summary>
    private static readonly Dictionary<string, Func<string>> RefusedTokenBuilders = new()
    {
        ["not a JWT"] = () => "not-a-token",
        ["three parts that are not a JWT"] = () => "abc.def.ghi",
        ["signed with a key that is not published"] = () => StaffTokens.Create(new TestSigningKey("unpublished-key")),
        ["signed with another key under the published key's name"] =
            () => StaffTokens.Create(new TestSigningKey(TestSigningKey.Default.KeyId)),
        ["a changed payload under the old signature"] = StaffTokens.WithChangedPayload,
        ["algorithm none"] = StaffTokens.CreateUnsigned,
        ["algorithm HS256 signed with the public key as the secret"] =
            () => StaffTokens.CreateHs256(Encoding.ASCII.GetBytes(TestSigningKey.Default.Rsa.ExportSubjectPublicKeyInfoPem())),
        ["iss of another team"] = () => StaffTokens.Create(payload: claims => claims["iss"] = "https://other.cloudflareaccess.com"),
        ["iss with a trailing slash"] = () => StaffTokens.Create(payload: claims => claims["iss"] = StaffTokens.TeamDomain + "/"),
        ["aud of another application as a string"] = () => StaffTokens.Create(payload: claims => claims["aud"] = "other-audience"),
        ["aud of another application as a list"] =
            () => StaffTokens.Create(payload: claims => claims["aud"] = new[] { "other-audience", "third-audience" }),
        ["aud with a trailing slash"] = () => StaffTokens.Create(payload: claims => claims["aud"] = StaffTokens.Audience + "/"),
        ["type org"] = () => StaffTokens.Create(payload: claims => claims["type"] = "org"),
        ["no type"] = () => StaffTokens.Create(payload: claims => claims.Remove("type")),
        ["no exp"] = () => StaffTokens.Create(payload: claims => claims.Remove("exp")),
        ["an exp more than 60 seconds ago"] = () => StaffTokens.Create(payload: claims =>
        {
            claims["iat"] = claims["nbf"] = SecondsFromNow(-600);
            claims["exp"] = SecondsFromNow(-75);
        }),
        ["an nbf more than 60 seconds ahead"] = () => StaffTokens.Create(payload: claims => claims["nbf"] = SecondsFromNow(75)),
    };

    /// <summary>The accepted tokens of behavior 4, by name, built as <see cref="RefusedTokenBuilders"/> are.</summary>
    private static readonly Dictionary<string, Func<string>> AcceptedTokenBuilders = new()
    {
        ["aud as a string"] = () => StaffTokens.Create(payload: claims => claims["aud"] = StaffTokens.Audience),
        ["aud as a list that holds the audience among others"] =
            () => StaffTokens.Create(payload: claims => claims["aud"] = new[] { "other-audience", StaffTokens.Audience, "third" }),
        ["an exp that passed less than 60 seconds ago"] = () => StaffTokens.Create(payload: claims =>
        {
            claims["iat"] = claims["nbf"] = SecondsFromNow(-600);
            claims["exp"] = SecondsFromNow(-45);
        }),
        ["an nbf less than 60 seconds ahead"] = () => StaffTokens.Create(payload: claims => claims["nbf"] = SecondsFromNow(45)),
        ["no nbf"] = () => StaffTokens.Create(payload: claims => claims.Remove("nbf")),
    };

    public static TheoryData<string> RefusedTokens => [.. RefusedTokenBuilders.Keys];

    public static TheoryData<string> AcceptedTokens => [.. AcceptedTokenBuilders.Keys];

    [Fact]
    public async Task Behavior_01_a_valid_token_for_the_configured_email_gets_the_answer_of_brief_001()
    {
        var gateway = FoundGateway();
        await using var factory = HostWith(gateway);

        using var response = await AccessRequest.SendAsync(factory, StaffTokens.Create());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(12, gateway.LastItemId!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Behavior_02_a_missing_or_empty_token_returns_401_with_the_challenge_and_koha_is_not_called(string? token)
    {
        var gateway = FoundGateway();
        await using var factory = HostWith(gateway);

        using var response = await AccessRequest.SendAsync(factory, token);

        await AssertUnauthenticatedAsync(response);
        Assert.Equal(0, gateway.Calls);
    }

    [Theory]
    [MemberData(nameof(RefusedTokens))]
    public async Task Behavior_03_a_token_that_fails_a_check_returns_401_and_koha_is_not_called(string reason)
    {
        var gateway = FoundGateway();
        await using var factory = HostWith(gateway);

        using var response = await AccessRequest.SendAsync(factory, RefusedTokenBuilders[reason]());

        await AssertUnauthenticatedAsync(response);
        Assert.True(gateway.Calls == 0, reason);
    }

    [Theory]
    [MemberData(nameof(AcceptedTokens))]
    public async Task Behavior_04_a_token_is_accepted(string reason)
    {
        await using var factory = HostWith(FoundGateway());

        using var response = await AccessRequest.SendAsync(factory, AcceptedTokenBuilders[reason]());

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{reason}: {response.StatusCode}");
    }

    [Theory]
    [InlineData("https://example.cloudflareaccess.com/")]
    [InlineData("https://EXAMPLE.CloudflareAccess.com")]
    [InlineData("https://Example.cloudflareaccess.com/")]
    [InlineData("https://example.cloudflareaccess.com:443")]
    public async Task Behavior_04_the_issuer_is_the_origin_of_the_team_domain_setting(string teamDomain)
    {
        await using var factory = HostWith(FoundGateway(), teamDomain);

        using var response = await AccessRequest.SendAsync(factory, StaffTokens.Create());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("https://localhost:8443", HttpStatusCode.OK)]
    [InlineData("https://localhost", HttpStatusCode.Unauthorized)]
    public async Task Behavior_04_a_port_other_than_443_is_part_of_the_issuer(string issuer, HttpStatusCode status)
    {
        await using var factory = HostWith(FoundGateway(), "https://localhost:8443");

        using var response = await AccessRequest.SendAsync(
            factory,
            StaffTokens.Create(payload: claims => claims["iss"] = issuer));

        Assert.Equal(status, response.StatusCode);
    }

    [Fact]
    public async Task Behavior_05_a_valid_token_in_the_cookie_or_the_authorization_header_returns_401()
    {
        var gateway = FoundGateway();
        await using var factory = HostWith(gateway);
        var token = StaffTokens.Create();

        using var inCookie = await AccessRequest.SendAsync(
            factory,
            token: null,
            headers: headers => headers.Add("Cookie", $"CF_Authorization={token}"));
        using var inBearer = await AccessRequest.SendAsync(
            factory,
            token: null,
            headers: headers => headers.Add("Authorization", $"Bearer {token}"));
        using var inBoth = await AccessRequest.SendAsync(
            factory,
            token: null,
            headers: headers =>
            {
                headers.Add("Cookie", $"CF_Authorization={token}");
                headers.Add("Authorization", $"Bearer {token}");
            });

        await AssertUnauthenticatedAsync(inCookie);
        await AssertUnauthenticatedAsync(inBearer);
        await AssertUnauthenticatedAsync(inBoth);
        Assert.Equal(0, gateway.Calls);
    }

    [Theory]
    [InlineData("other.person@example.org")]
    [InlineData(StaffTokens.Email + "x")]
    [InlineData("x" + StaffTokens.Email)]
    [InlineData("")]
    [InlineData(null)]
    public async Task Behavior_06_another_email_returns_403_access_denied_and_koha_is_not_called(string? email)
    {
        var gateway = FoundGateway();
        await using var factory = HostWith(gateway);

        using var response = await AccessRequest.SendAsync(factory, TokenWithEmail(email));

        await AssertAccessDeniedAsync(response);
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task Behavior_06_a_token_with_two_emails_returns_403_access_denied()
    {
        await using var factory = HostWith(FoundGateway());

        using var response = await AccessRequest.SendAsync(
            factory,
            StaffTokens.Create(payload: claims => claims["email"] = new[] { StaffTokens.Email, "other.person@example.org" }));

        await AssertAccessDeniedAsync(response);
    }

    [Theory]
    [InlineData("STAFF@EXAMPLE.ORG")]
    [InlineData("Staff@Example.org")]
    public async Task Behavior_07_an_email_that_differs_only_in_letter_case_is_accepted(string email)
    {
        await using var factory = HostWith(FoundGateway());

        using var response = await AccessRequest.SendAsync(factory, TokenWithEmail(email));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Behavior_07_a_configured_email_with_capital_letters_accepts_the_lower_case_email()
    {
        await using var factory = HostWith(
            FoundGateway(),
            settings: new Dictionary<string, string?> { ["MINIOASIS_CLOUDFLARE_ACCESS_EMAIL"] = "Staff@Example.ORG" });

        using var response = await AccessRequest.SendAsync(factory, StaffTokens.Create());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(StaffTokens.Email)]
    [InlineData("other.person@example.org")]
    [InlineData(null)]
    public async Task Behavior_08_a_token_that_fails_a_check_returns_401_not_403_whatever_its_email(string? email)
    {
        await using var factory = HostWith(FoundGateway());
        var expired = StaffTokens.Create(payload: claims =>
        {
            SetEmail(claims, email);
            claims["iat"] = claims["nbf"] = SecondsFromNow(-600);
            claims["exp"] = SecondsFromNow(-75);
        });
        var wrongAudience = StaffTokens.Create(payload: claims =>
        {
            SetEmail(claims, email);
            claims["aud"] = "other-audience";
        });
        var unpublishedKey = StaffTokens.Create(new TestSigningKey("unpublished-key"), claims => SetEmail(claims, email));

        foreach (var token in new[] { expired, wrongAudience, unpublishedKey })
        {
            using var response = await AccessRequest.SendAsync(factory, token);
            await AssertUnauthenticatedAsync(response);
        }
    }

    [Theory]
    [InlineData("/nope")]
    [InlineData("/api/v1/items/12/extra")]
    public async Task Behavior_09_an_unknown_path_returns_401_without_a_valid_token_and_404_with_one(string path)
    {
        await using var factory = HostWith(FoundGateway());

        using var without = await AccessRequest.SendAsync(factory, token: null, path: path);
        using var invalid = await AccessRequest.SendAsync(factory, "not-a-token", path: path);
        using var valid = await AccessRequest.SendAsync(factory, StaffTokens.Create(), path: path);

        await AssertUnauthenticatedAsync(without);
        await AssertUnauthenticatedAsync(invalid);
        await ProblemAssert.HasShapeAsync(valid, HttpStatusCode.NotFound, "not_found", "Not found", "The requested resource does not exist.");
    }

    [Fact]
    public async Task Behavior_09_a_method_the_route_does_not_allow_returns_401_without_a_valid_token_and_405_with_one()
    {
        await using var factory = HostWith(FoundGateway());

        using var without = await AccessRequest.SendAsync(factory, token: null, method: HttpMethod.Post, path: "/api/v1/items/1");
        using var valid = await AccessRequest.SendAsync(factory, StaffTokens.Create(), method: HttpMethod.Post, path: "/api/v1/items/1");

        await AssertUnauthenticatedAsync(without);
        Assert.Empty(without.Content.Headers.Allow);
        await ProblemAssert.HasShapeAsync(
            valid,
            HttpStatusCode.MethodNotAllowed,
            "method_not_allowed",
            "Method not allowed",
            "This resource does not support the request method.");
    }

    [Fact]
    public async Task Behavior_15_no_log_line_and_no_response_holds_a_token_its_payload_or_an_email()
    {
        const string TokenEmail = "other.person@example.org";
        await using var factory = HostWith(FoundGateway());
        var valid = StaffTokens.Create();
        var expired = StaffTokens.Create(payload: claims =>
        {
            claims["email"] = TokenEmail;
            claims["iat"] = claims["nbf"] = SecondsFromNow(-600);
            claims["exp"] = SecondsFromNow(-75);
        });
        var wrongAudience = StaffTokens.Create(payload: claims =>
        {
            claims["email"] = TokenEmail;
            claims["aud"] = "other-audience";
        });
        var unpublishedKey = StaffTokens.Create(new TestSigningKey("unpublished-key"), claims => claims["email"] = TokenEmail);
        var otherEmail = TokenWithEmail(TokenEmail);
        string[] tokens = [valid, expired, wrongAudience, unpublishedKey, otherEmail, "not-a-token"];

        var responses = new List<HttpResponseMessage>
        {
            await AccessRequest.SendAsync(factory, valid),
            await AccessRequest.SendAsync(factory, token: null),
            await AccessRequest.SendAsync(factory, expired),
            await AccessRequest.SendAsync(factory, wrongAudience),
            await AccessRequest.SendAsync(factory, unpublishedKey),
            await AccessRequest.SendAsync(factory, "not-a-token"),
            await AccessRequest.SendAsync(factory, otherEmail),
            await AccessRequest.SendAsync(factory, valid, requestedWith: null),
        };

        Assert.Equal(
            [
                HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized,
                HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden,
            ],
            responses.Select(response => response.StatusCode));
        var secrets = tokens
            .Concat(tokens.Where(token => token.Contains('.', StringComparison.Ordinal)).Select(StaffTokens.PayloadOf))
            .Append(TokenEmail)
            .Append(StaffTokens.Email)
            .ToList();
        var written = new List<string>();
        foreach (var response in responses)
        {
            written.Add(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            written.AddRange(response.Headers.Concat(response.Content.Headers)
                .SelectMany(header => header.Value.Select(value => $"{header.Key}: {value}")));
            response.Dispose();
        }

        written.AddRange(factory.Logs.Entries.SelectMany(entry =>
            new[] { entry.Message, entry.Exception?.ToString() ?? string.Empty }
                .Concat(entry.Values.Values.Select(value => value?.ToString() ?? string.Empty))));
        Assert.NotEmpty(factory.Logs.Entries);
        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(written, text => text.Contains(secret, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task Behavior_15_a_401_says_nothing_about_which_check_failed()
    {
        await using var factory = HostWith(FoundGateway());
        var tokens = new[]
        {
            null,
            "not-a-token",
            StaffTokens.Create(payload: claims =>
            {
                claims["iat"] = claims["nbf"] = SecondsFromNow(-600);
                claims["exp"] = SecondsFromNow(-75);
            }),
            StaffTokens.Create(payload: claims => claims["aud"] = "other-audience"),
            StaffTokens.Create(new TestSigningKey("unpublished-key")),
        };

        var bodies = new List<string>();
        foreach (var token in tokens)
        {
            using var response = await AccessRequest.SendAsync(factory, token);
            var body = await AssertUnauthenticatedAsync(response);
            bodies.Add(body.GetRawText().Replace(body.GetProperty("traceId").GetString()!, string.Empty, StringComparison.Ordinal));
        }

        Assert.Single(bodies.Distinct(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("GET", null)]
    [InlineData("GET", "")]
    [InlineData("GET", "xmlhttprequest")]
    [InlineData("GET", "XMLHttpRequest ")]
    [InlineData("GET", "fetch")]
    [InlineData("POST", null)]
    [InlineData("POST", "XMLHTTPREQUEST")]
    public async Task Behavior_17_without_x_requested_with_exactly_xmlhttprequest_returns_403_request_refused(
        string method,
        string? requestedWith)
    {
        var gateway = FoundGateway();
        await using var factory = HostWith(gateway);

        using var response = await AccessRequest.SendAsync(
            factory,
            StaffTokens.Create(),
            requestedWith,
            new HttpMethod(method));

        await ProblemAssert.HasShapeAsync(
            response,
            HttpStatusCode.Forbidden,
            "request_refused",
            "Request refused",
            "This request did not come from Minioasis.");
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task Behavior_17_x_requested_with_given_twice_returns_403_request_refused()
    {
        await using var factory = HostWith(FoundGateway());

        using var response = await AccessRequest.SendAsync(
            factory,
            StaffTokens.Create(),
            headers: headers => headers.Add(ApiTestHostFactory.RequestedWithHeader, ApiTestHostFactory.RequestedWithValue));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(ApiTestHostFactory.RequestedWithValue)]
    [InlineData("fetch")]
    public async Task Behavior_18_the_token_and_the_email_are_checked_before_x_requested_with(string? requestedWith)
    {
        await using var factory = HostWith(FoundGateway());

        using var withoutToken = await AccessRequest.SendAsync(factory, token: null, requestedWith);
        using var invalidToken = await AccessRequest.SendAsync(factory, "not-a-token", requestedWith);
        using var otherEmail = await AccessRequest.SendAsync(factory, TokenWithEmail("other.person@example.org"), requestedWith);

        await AssertUnauthenticatedAsync(withoutToken);
        await AssertUnauthenticatedAsync(invalidToken);
        await AssertAccessDeniedAsync(otherEmail);
    }

    [Fact]
    public async Task A_refused_request_is_logged_with_its_reason_and_the_trace_id_of_the_response()
    {
        await using var factory = HostWith(FoundGateway());

        using var unauthenticated = await AccessRequest.SendAsync(factory, token: null);
        using var denied = await AccessRequest.SendAsync(factory, TokenWithEmail("other.person@example.org"));
        using var refused = await AccessRequest.SendAsync(factory, StaffTokens.Create(), requestedWith: null);

        foreach (var (response, code) in new[]
        {
            (unauthenticated, "unauthenticated"), (denied, "access_denied"), (refused, "request_refused"),
        })
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
            var traceId = body.RootElement.GetProperty("traceId").GetString();
            Assert.Single(factory.Logs.Entries, entry =>
                entry.Values.TryGetValue("ErrorCode", out var logged) && Equals(logged, code)
                && entry.Values.TryGetValue("TraceId", out var loggedTraceId) && Equals(loggedTraceId, traceId));
        }
    }

    [Fact]
    public async Task An_accepted_request_logs_nothing_at_warning_or_above()
    {
        await using var factory = HostWith(FoundGateway());

        using var response = await AccessRequest.SendAsync(factory, StaffTokens.Create());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(factory.Logs.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    private static FakeGetItemGateway FoundGateway() =>
        new(GetItemGatewayResult.Found(new GetItemResponse("12", "7", "BC-1", "QA 76")));

    private static ApiTestHostFactory HostWith(
        IGetItemGateway gateway,
        string? teamDomain = null,
        Dictionary<string, string?>? settings = null)
    {
        settings ??= [];
        if (teamDomain is not null)
        {
            settings["MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN"] = teamDomain;
        }

        return new ApiTestHostFactory(
            services =>
            {
                services.RemoveAll<IGetItemGateway>();
                services.AddSingleton(gateway);
            },
            settings);
    }

    private static string TokenWithEmail(string? email) => StaffTokens.Create(payload: claims => SetEmail(claims, email));

    private static void SetEmail(Dictionary<string, object?> claims, string? email)
    {
        if (email is null)
        {
            claims.Remove("email");
        }
        else
        {
            claims["email"] = email;
        }
    }

    private static long SecondsFromNow(int seconds) => DateTimeOffset.UtcNow.AddSeconds(seconds).ToUnixTimeSeconds();

    private static async Task<JsonElement> AssertUnauthenticatedAsync(HttpResponseMessage response)
    {
        var body = await ProblemAssert.HasShapeAsync(
            response,
            HttpStatusCode.Unauthorized,
            "unauthenticated",
            UnauthenticatedTitle,
            UnauthenticatedDetail);
        Assert.Equal(["CloudflareAccess"], response.Headers.GetValues("WWW-Authenticate"));
        return body;
    }

    private static Task<JsonElement> AssertAccessDeniedAsync(HttpResponseMessage response) =>
        ProblemAssert.HasShapeAsync(
            response,
            HttpStatusCode.Forbidden,
            "access_denied",
            "Access denied",
            "This account may not use Minioasis.");
}
