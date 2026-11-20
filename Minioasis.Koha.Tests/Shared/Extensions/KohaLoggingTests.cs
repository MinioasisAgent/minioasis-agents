using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minioasis.Koha.Authentication.Exceptions;
using Minioasis.Koha.Authentication.Providers;
using Minioasis.Koha.Shared.Clients;
using Minioasis.Koha.Shared.Exceptions;
using Minioasis.Koha.Tests.Shared.TestDoubles;
using System.Net;
using Xunit;

namespace Minioasis.Koha.Tests.Shared.Extensions;

/// <summary>
/// Spec 001, R23 and the Koha clause of R5, built from <c>AddKohaIntegration</c> with every category captured at
/// <c>Trace</c>: the token request, the protected call with its 401 retry, the header redaction, and the levels the
/// Koha connection writes at.
/// </summary>
public sealed class KohaLoggingTests
{
    private const string ClientSecret = KohaClientsHarness.ClientSecret;
    private const string AccessToken = "access-token-8d2b71";
    private const string NewAccessToken = "access-token-new-4c19e0";
    private const string Marker = "koha-body-marker-3a6e";

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private const string SuccessBody =
        $$"""{"access_token":"{{AccessToken}}","token_type":"Bearer","expires_in":3600}""";

    // Failing token replies, each carrying the marker or the access token or both: Koha error bodies under statuses
    // that the contract defines (400, 403) and does not (401, 500, 503), and 200 replies that fail R16 or cannot be
    // parsed.
    private static readonly (int Status, string Body)[] Failures =
    [
        (400, $$"""{"error":"{{Marker}}","error_code":"{{Marker}}"}"""),
        (401, $"{Marker} {AccessToken}"),
        (403, $$"""{"error":"{{Marker}}"}"""),
        (500, $"<html>{Marker} {AccessToken}</html>"),
        (503, $$"""{"error":"{{Marker}}","access_token":"{{AccessToken}}"}"""),
        (200, $$"""{"access_token":"{{AccessToken}}","token_type":"mac","expires_in":3600,"note":"{{Marker}}"}"""),
        (200, $$"""{"access_token":"{{AccessToken}}","token_type":"Bearer","expires_in":"{{Marker}}"}"""),
        (200, $$"""{"access_token":"{{AccessToken}}","note":"{{Marker}}"""),
        (200, $"{Marker} {AccessToken}"),
    ];

    public static TheoryData<int, string> FailingReplies => Data(Failures);

    public static TheoryData<int, string> Replies => Data([(200, SuccessBody), .. Failures]);

    // R23: whatever the token reply, no entry and no scope of any category, HttpClient's included, contains the
    // client secret, the access token, or any part of Koha's body.
    [Theory]
    [MemberData(nameof(Replies))]
    public async Task No_entry_contains_secret_token_header_or_body(int status, string body)
    {
        var logs = new RecordingLoggerProvider();
        using var harness = Harness(logs, StubHttpMessageHandler.Reply((HttpStatusCode)status, body));

        await Record.ExceptionAsync(() => harness.Services.GetRequiredService<KohaAccessTokenProvider>()
            .GetTokenAsync(TestContext.Current.CancellationToken)
            .WaitAsync(Wait, TestContext.Current.CancellationToken));

        // The capture works: HttpClient's own entries for the token request are recorded.
        Assert.Contains(
            logs.Entries,
            entry => entry.Category.StartsWith($"System.Net.Http.HttpClient.{KohaClientNames.OAuth}.", StringComparison.Ordinal));
        Assert.All(logs.Texts, text =>
        {
            Assert.DoesNotContain(ClientSecret, text, StringComparison.Ordinal);
            Assert.DoesNotContain(AccessToken, text, StringComparison.Ordinal);
            Assert.DoesNotContain(Marker, text, StringComparison.Ordinal);
        });
    }

    // R23, plan "Approach": the documented Koha exceptions carry Koha's status, and neither Koha's body, nor the
    // token, nor the generated client's exception (which holds the body).
    [Theory]
    [MemberData(nameof(FailingReplies))]
    public async Task Koha_exceptions_carry_no_body(int status, string body)
    {
        using var harness = Harness(new RecordingLoggerProvider(), StubHttpMessageHandler.Reply((HttpStatusCode)status, body));

        var exception = await Record.ExceptionAsync(() => harness.Services.GetRequiredService<KohaAccessTokenProvider>()
            .GetTokenAsync(TestContext.Current.CancellationToken)
            .WaitAsync(Wait, TestContext.Current.CancellationToken));

        var kohaStatus = exception switch
        {
            KohaAuthenticationException authentication => authentication.KohaStatus,
            KohaUnavailableException unavailable => unavailable.KohaStatus,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected exception: {exception}"),
        };
        Assert.Equal(status, kohaStatus);
        Assert.Null(exception.InnerException);
        var text = exception.ToString();
        Assert.DoesNotContain(Marker, text, StringComparison.Ordinal);
        Assert.DoesNotContain(AccessToken, text, StringComparison.Ordinal);
        Assert.DoesNotContain(ClientSecret, text, StringComparison.Ordinal);
    }

    // R23, protected call: a successful call, a 401 followed by the retry with a new token, and calls that fail with
    // a Koha error body holding the marker. No entry and no scope of any category contains the client secret, either
    // access token, or the marker. HttpClient's own entries for the API client are captured, and they name the
    // Authorization header, so the header was logged with its value redacted, not left out.
    [Theory]
    [InlineData("success")]
    [InlineData("401 retry")]
    [InlineData("error body 400")]
    [InlineData("error body 500")]
    public async Task No_entry_of_a_protected_call_contains_secret_token_header_or_body(string scenario)
    {
        var logs = new RecordingLoggerProvider();
        using var harness = ProtectedCallHarness(logs, scenario);

        await CallAsync(harness);

        AssertNothingSecretWasLogged(logs);
    }

    // R23, ER-008: the Koha clients redact every header value themselves, so an application-wide default that logs
    // header values does not reach them. (.NET 10 already redacts by default; this test switches that default off.)
    [Fact]
    public async Task Header_values_stay_redacted_when_the_application_default_logs_them()
    {
        var logs = new RecordingLoggerProvider();
        using var harness = ProtectedCallHarness(
            logs,
            "401 retry",
            services => services.ConfigureHttpClientDefaults(builder => builder.RedactLoggedHeaders(_ => false)));

        await CallAsync(harness);

        AssertNothingSecretWasLogged(logs);
    }

    // R5 (Koha clause), R22: the Koha connection writes no entry at Warning or above, in any category, when the token
    // request is refused, when Koha cannot be reached, when the deadline expires, when Koha answers 5xx, or when the
    // caller cancels. The single Error entry for a server failure is Api's (R5).
    [Theory]
    [InlineData("token refusal")]
    [InlineData("network")]
    [InlineData("deadline")]
    [InlineData("5xx")]
    [InlineData("cancellation")]
    public async Task Koha_writes_nothing_at_warning_or_above(string scenario)
    {
        var logs = new RecordingLoggerProvider();
        var tokens = new StubHttpMessageHandler((_, _) => scenario switch
        {
            "token refusal" => Task.FromResult(
                StubHttpMessageHandler.Reply(HttpStatusCode.BadRequest, """{"error":"invalid_client"}""")),
            "network" => Task.FromException<HttpResponseMessage>(
                new HttpRequestException("The test connection failed.")),
            _ => Task.FromResult(TokenReply(AccessToken)),
        });
        var api = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            if (scenario == "5xx")
            {
                return StubHttpMessageHandler.Reply(HttpStatusCode.ServiceUnavailable, $"{Marker}");
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Not reached.");
        });
        using var harness = new KohaClientsHarness(tokens, api, logs);
        using var caller = new CancellationTokenSource();

        var call = harness.CreateApiClient().GetAsync(
            new Uri(KohaClientsHarness.BaseUrl + "patrons/7"),
            caller.Token);
        if (scenario is "deadline" or "cancellation")
        {
            await api.WaitForRequestsAsync(1);
            if (scenario == "deadline")
            {
                harness.Time.Advance(KohaClientsHarness.Deadline);
            }
            else
            {
                await caller.CancelAsync();
            }
        }

        var outcome = await Record.ExceptionAsync(() => call.WaitAsync(Wait, TestContext.Current.CancellationToken));

        // The scenario ended as it should, so the entries below are those of that outcome.
        Assert.True(scenario switch
        {
            "token refusal" => outcome is KohaAuthenticationException,
            "network" or "deadline" => outcome is KohaUnavailableException,
            "5xx" => outcome is null,
            _ => outcome is OperationCanceledException,
        });
        Assert.NotEmpty(logs.Entries);
        Assert.DoesNotContain(logs.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    // A protected GET whose token is AccessToken, and after a 401 NewAccessToken. Koha's replies to the call hold the
    // marker, and in the error scenarios a token too.
    private static KohaClientsHarness ProtectedCallHarness(
        RecordingLoggerProvider logs,
        string scenario,
        Action<IServiceCollection>? configure = null)
    {
        var tokens = new StubHttpMessageHandler((request, _) => Task.FromResult(
            TokenReply(request.Index == 0 ? AccessToken : NewAccessToken)));
        var api = new StubHttpMessageHandler((request, _) => Task.FromResult((scenario, request.Index) switch
        {
            ("401 retry", 0) => StubHttpMessageHandler.Reply(HttpStatusCode.Unauthorized, $$"""{"error":"{{Marker}}"}"""),
            ("error body 400", _) => StubHttpMessageHandler.Reply(
                HttpStatusCode.BadRequest,
                $$"""{"error":"{{Marker}}","token":"{{AccessToken}}"}"""),
            ("error body 500", _) => StubHttpMessageHandler.Reply(
                HttpStatusCode.InternalServerError,
                $"<html>{Marker} {AccessToken}</html>"),
            _ => StubHttpMessageHandler.Reply(HttpStatusCode.OK, $$"""{"name":"{{Marker}}"}"""),
        }));
        return new KohaClientsHarness(tokens, api, logs, configure);
    }

    // Sends the call as a generated client would, and reads the body.
    private static async Task CallAsync(KohaClientsHarness harness)
    {
        using var response = await harness.CreateApiClient()
            .SendAsync(
                new HttpRequestMessage(HttpMethod.Get, new Uri(KohaClientsHarness.BaseUrl + "patrons/7")),
                HttpCompletionOption.ResponseHeadersRead,
                TestContext.Current.CancellationToken)
            .WaitAsync(Wait, TestContext.Current.CancellationToken);
        Assert.Contains(Marker, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static void AssertNothingSecretWasLogged(RecordingLoggerProvider logs)
    {
        var apiEntries = logs.Entries
            .Where(entry => entry.Category.StartsWith(
                $"System.Net.Http.HttpClient.{KohaClientNames.Api}.",
                StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(apiEntries);
        Assert.Contains(apiEntries, entry => entry.Message.Contains("Authorization", StringComparison.Ordinal));
        Assert.All(logs.Texts, text =>
        {
            Assert.DoesNotContain(ClientSecret, text, StringComparison.Ordinal);
            Assert.DoesNotContain(AccessToken, text, StringComparison.Ordinal);
            Assert.DoesNotContain(NewAccessToken, text, StringComparison.Ordinal);
            Assert.DoesNotContain(Marker, text, StringComparison.Ordinal);
        });
    }

    private static HttpResponseMessage TokenReply(string accessToken) =>
        StubHttpMessageHandler.Reply(
            HttpStatusCode.OK,
            $$"""{"access_token":"{{accessToken}}","token_type":"Bearer","expires_in":3600}""");

    private static TheoryData<int, string> Data(IEnumerable<(int Status, string Body)> replies)
    {
        var data = new TheoryData<int, string>();
        foreach (var (status, body) in replies)
        {
            data.Add(status, body);
        }

        return data;
    }

    private static KohaClientsHarness Harness(RecordingLoggerProvider logs, HttpResponseMessage reply) =>
        new(new StubHttpMessageHandler(() => reply), new StubHttpMessageHandler(() => new HttpResponseMessage()), logs);
}
