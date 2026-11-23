using Microsoft.Extensions.Logging;
using Minioasis.Koha.Authentication.Exceptions;
using Minioasis.Koha.Tests.Shared.TestDoubles;
using System.Net;
using System.Text;
using Xunit;

namespace Minioasis.Koha.Tests.Authentication.Providers;

public sealed class KohaAccessTokenProviderTests
{
    [Fact]
    public async Task Sends_the_grant_type_in_the_body_and_the_credentials_in_the_basic_header()
    {
        using var host = new KohaTestHost();

        var token = await host.Tokens.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal("token-1", token);
        var request = Assert.Single(host.OAuth.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("https://koha.test/api/v1/oauth/token"), request.Uri);
        Assert.Equal("application/x-www-form-urlencoded", request.MediaType);
        Assert.Contains("grant_type=client_credentials", request.Body!.Split('&'));
        Assert.DoesNotContain(KohaTestHost.ClientSecret, request.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(Uri.EscapeDataString(KohaTestHost.ClientSecret), request.Body, StringComparison.Ordinal);
        Assert.Equal("Basic", request.Authorization!.Scheme);
        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes("test-client:test-secret-value")),
            request.Authorization.Parameter);
    }

    [Fact]
    public async Task Encodes_the_basic_credentials_as_utf_8()
    {
        using var host = new KohaTestHost(clientId: "clïent", clientSecret: "sécret");

        await host.Tokens.GetAsync(TestContext.Current.CancellationToken);

        var request = Assert.Single(host.OAuth.Requests);
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("clïent:sécret")), request.Authorization!.Parameter);
        Assert.DoesNotContain("sécret", request.Body!, StringComparison.Ordinal);
        Assert.DoesNotContain(Uri.EscapeDataString("sécret"), request.Body!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Bearer")]
    [InlineData("bearer")]
    [InlineData("BEARER")]
    [InlineData("bEaReR")]
    public async Task Accepts_a_bearer_token_in_any_letter_case(string tokenType)
    {
        using var host = new KohaTestHost(oauth: (_, _) => Task.FromResult(KohaResponses.Token(tokenType: tokenType)));

        Assert.Equal("token-1", await host.Tokens.GetAsync(TestContext.Current.CancellationToken));
    }

    public static TheoryData<string> RejectedTokenResponses => new()
    {
        """{"access_token":"","token_type":"Bearer","expires_in":3600}""",
        """{"access_token":"   ","token_type":"Bearer","expires_in":3600}""",
        """{"access_token":null,"token_type":"Bearer","expires_in":3600}""",
        """{"token_type":"Bearer","expires_in":3600}""",
        """{"access_token":"t","token_type":"Bearer","expires_in":0}""",
        """{"access_token":"t","token_type":"Bearer","expires_in":-1}""",
        """{"access_token":"t","token_type":"Bearer"}""",
        """{"access_token":"t","token_type":"MAC","expires_in":3600}""",
        """{"access_token":"t","token_type":"Bearer2","expires_in":3600}""",
        """{"access_token":"t","token_type":"","expires_in":3600}""",
        """{"access_token":"t","expires_in":3600}""",
    };

    [Theory]
    [MemberData(nameof(RejectedTokenResponses))]
    public async Task Rejects_a_token_response_that_is_not_a_usable_bearer_token(string json)
    {
        using var host = new KohaTestHost(oauth: (_, _) => Task.FromResult(KohaResponses.Json(HttpStatusCode.OK, json)));

        await Assert.ThrowsAsync<KohaAuthenticationException>(
            () => host.Tokens.GetAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reuses_the_token_until_60_seconds_before_it_expires()
    {
        var issued = 0;
        using var host = new KohaTestHost(oauth: (_, _) =>
            Task.FromResult(KohaResponses.Token(token: $"token-{++issued}", expiresIn: 3600)));
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal("token-1", await host.Tokens.GetAsync(cancellationToken));
        Assert.Equal("token-1", await host.Tokens.GetAsync(cancellationToken));

        host.Time.Advance(TimeSpan.FromSeconds(3540) - TimeSpan.FromTicks(1));
        Assert.Equal("token-1", await host.Tokens.GetAsync(cancellationToken));
        Assert.Single(host.OAuth.Requests);

        host.Time.Advance(TimeSpan.FromTicks(1));
        Assert.Equal("token-2", await host.Tokens.GetAsync(cancellationToken));
        Assert.Equal("token-2", await host.Tokens.GetAsync(cancellationToken));
        Assert.Equal(2, host.OAuth.Requests.Count);
    }

    [Fact]
    public async Task Requests_a_new_token_for_each_caller_when_the_lifetime_is_within_the_margin()
    {
        var issued = 0;
        using var host = new KohaTestHost(oauth: (_, _) =>
            Task.FromResult(KohaResponses.Token(token: $"token-{++issued}", expiresIn: 60)));

        Assert.Equal("token-1", await host.Tokens.GetAsync(TestContext.Current.CancellationToken));
        Assert.Equal("token-2", await host.Tokens.GetAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Callers_at_the_same_moment_share_one_token_request()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var host = new KohaTestHost(oauth: async (_, _) =>
        {
            await release.Task;
            return KohaResponses.Token();
        });

        var callers = Enumerable.Range(0, 5)
            .Select(_ => host.Tokens.GetAsync(TestContext.Current.CancellationToken))
            .ToList();
        await Wait.UntilAsync(() => host.OAuth.Requests.Count == 1);
        release.SetResult();

        var tokens = await Task.WhenAll(callers).WaitAsync(Wait.Limit, TestContext.Current.CancellationToken);
        Assert.All(tokens, token => Assert.Equal("token-1", token));
        Assert.Single(host.OAuth.Requests);
    }

    [Fact]
    public async Task One_caller_cancelling_does_not_cancel_the_shared_request_for_the_others()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var host = new KohaTestHost(oauth: async (_, token) =>
        {
            await release.Task.WaitAsync(token);
            return KohaResponses.Token();
        });
        using var leaving = new CancellationTokenSource();

        var cancelled = host.Tokens.GetAsync(leaving.Token);
        var waiting = host.Tokens.GetAsync(TestContext.Current.CancellationToken);
        await Wait.UntilAsync(() => host.OAuth.Requests.Count == 1);
        await leaving.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.WaitAsync(Wait.Limit, TestContext.Current.CancellationToken));
        release.SetResult();
        Assert.Equal("token-1", await waiting.WaitAsync(Wait.Limit, TestContext.Current.CancellationToken));
        Assert.Single(host.OAuth.Requests);
    }

    [Fact]
    public async Task The_shared_request_is_bounded_by_the_10_second_deadline()
    {
        using var host = new KohaTestHost(oauth: async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return KohaResponses.Token();
        });

        var waiting = host.Tokens.GetAsync(CancellationToken.None);
        await Wait.UntilAsync(() => host.OAuth.Requests.Count == 1);
        await host.Time.WaitForPendingTimersAsync(1);

        host.Time.Advance(TimeSpan.FromSeconds(10) - TimeSpan.FromTicks(1));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(waiting.IsCompleted);

        host.Time.Advance(TimeSpan.FromTicks(1));
        await Assert.ThrowsAsync<KohaAuthenticationException>(() => waiting.WaitAsync(Wait.Limit, TestContext.Current.CancellationToken));
    }

    public static TheoryData<string, Func<HttpResponseMessage>> FailedTokenAnswers => new()
    {
        { "403 access_denied", () => KohaResponses.Json(HttpStatusCode.Forbidden, """{"error":"access_denied"}""", KohaResponses.Utf8WithoutHyphen) },
        { "400 invalid_request", () => KohaResponses.Json(HttpStatusCode.BadRequest, """{"error":"invalid_request"}""", KohaResponses.Utf8WithoutHyphen) },
        { "401", () => KohaResponses.Json(HttpStatusCode.Unauthorized, """{"error":"unauthorized_client"}""", KohaResponses.Utf8WithoutHyphen) },
        { "500", () => KohaResponses.Json(HttpStatusCode.InternalServerError, """{"error":"Something went wrong"}""") },
        { "503 HTML", () => KohaResponses.Json(HttpStatusCode.ServiceUnavailable, "<html>down</html>", "text/html") },
        { "302", () => KohaResponses.Status(HttpStatusCode.Found) },
        { "200 unreadable", () => KohaResponses.Json(HttpStatusCode.OK, "<html>not json</html>", "text/html") },
        { "200 null", () => KohaResponses.Json(HttpStatusCode.OK, "null") },
        { "200 rejected", () => KohaResponses.Token(tokenType: "MAC") },
    };

    [Theory]
    [MemberData(nameof(FailedTokenAnswers))]
    public async Task Reports_each_failed_answer_as_an_authentication_failure_without_retrying(
        string name,
        Func<HttpResponseMessage> answer)
    {
        using var host = new KohaTestHost(oauth: (_, _) => Task.FromResult(answer()));

        await Assert.ThrowsAsync<KohaAuthenticationException>(
            () => host.Tokens.GetAsync(TestContext.Current.CancellationToken));

        Assert.True(host.OAuth.Requests.Count == 1, name);
        Assert.Contains(host.Logs.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Reports_a_connection_failure_as_an_authentication_failure()
    {
        using var host = new KohaTestHost(oauth: (_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException(HttpRequestError.ConnectionError, "Connection refused.")));

        await Assert.ThrowsAsync<KohaAuthenticationException>(
            () => host.Tokens.GetAsync(TestContext.Current.CancellationToken));
        Assert.Single(host.OAuth.Requests);
    }

    [Fact]
    public async Task Reports_a_timeout_of_the_token_request_as_an_authentication_failure()
    {
        using var host = new KohaTestHost(oauth: (_, _) =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException("The request timed out.", new TimeoutException())));

        await Assert.ThrowsAsync<KohaAuthenticationException>(
            () => host.Tokens.GetAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Tries_again_on_the_next_call_after_a_failure()
    {
        var answers = new Queue<HttpResponseMessage>(
        [
            KohaResponses.Json(HttpStatusCode.InternalServerError, "{}"),
            KohaResponses.Token(),
        ]);
        using var host = new KohaTestHost(oauth: (_, _) => Task.FromResult(answers.Dequeue()));

        await Assert.ThrowsAsync<KohaAuthenticationException>(
            () => host.Tokens.GetAsync(TestContext.Current.CancellationToken));
        Assert.Equal("token-1", await host.Tokens.GetAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, host.OAuth.Requests.Count);
    }

    [Fact]
    public async Task Logs_the_status_the_oauth_error_and_the_client_identifier()
    {
        using var host = new KohaTestHost(oauth: (_, _) => Task.FromResult(
            KohaResponses.Json(HttpStatusCode.Forbidden, """{"error":"access_denied"}""", KohaResponses.Utf8WithoutHyphen)));

        await Assert.ThrowsAsync<KohaAuthenticationException>(
            () => host.Tokens.GetAsync(TestContext.Current.CancellationToken));

        var entry = Assert.Single(host.Logs.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Equal(403, entry.Values["StatusCode"]);
        Assert.Equal("access_denied", entry.Values["OAuthError"]);
        Assert.Equal(KohaTestHost.ClientId, entry.Values["ClientId"]);
    }

    [Theory]
    [InlineData(65)]
    [InlineData(500)]
    public async Task Does_not_log_an_oauth_error_longer_than_64_characters(int length)
    {
        var error = new string('e', length);
        using var host = new KohaTestHost(oauth: (_, _) => Task.FromResult(
            KohaResponses.Json(HttpStatusCode.Forbidden, $$"""{"error":"{{error}}"}""", KohaResponses.Utf8WithoutHyphen)));

        await Assert.ThrowsAsync<KohaAuthenticationException>(
            () => host.Tokens.GetAsync(TestContext.Current.CancellationToken));

        var entry = Assert.Single(host.Logs.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Null(entry.Values["OAuthError"]);
        Assert.DoesNotContain(host.Logs.Entries, entry => entry.AllText.Contains(error, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("access\ndenied")]
    [InlineData("access\"denied")]
    [InlineData("accès_refusé")]
    public async Task Does_not_log_an_oauth_error_with_characters_that_rfc_6749_does_not_allow(string error)
    {
        using var host = new KohaTestHost(oauth: (_, _) => Task.FromResult(KohaResponses.Json(
            HttpStatusCode.Forbidden,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                error
            }),
            KohaResponses.Utf8WithoutHyphen)));

        await Assert.ThrowsAsync<KohaAuthenticationException>(
            () => host.Tokens.GetAsync(TestContext.Current.CancellationToken));

        var entry = Assert.Single(host.Logs.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Null(entry.Values["OAuthError"]);
    }

    [Fact]
    public async Task Discards_the_rejected_token_so_that_the_next_caller_gets_a_new_one()
    {
        var issued = 0;
        using var host = new KohaTestHost(oauth: (_, _) => Task.FromResult(KohaResponses.Token(token: $"token-{++issued}")));
        var cancellationToken = TestContext.Current.CancellationToken;

        var first = await host.Tokens.GetAsync(cancellationToken);
        host.Tokens.Invalidate(first);

        Assert.Equal("token-2", await host.Tokens.GetAsync(cancellationToken));
    }

    [Fact]
    public async Task Keeps_a_newer_token_when_an_older_one_is_rejected()
    {
        var issued = 0;
        using var host = new KohaTestHost(oauth: (_, _) => Task.FromResult(KohaResponses.Token(token: $"token-{++issued}")));
        var cancellationToken = TestContext.Current.CancellationToken;

        var first = await host.Tokens.GetAsync(cancellationToken);
        host.Tokens.Invalidate(first);
        var second = await host.Tokens.GetAsync(cancellationToken);
        host.Tokens.Invalidate(first);

        Assert.Equal(second, await host.Tokens.GetAsync(cancellationToken));
        Assert.Equal(2, host.OAuth.Requests.Count);
    }

    [Fact]
    public async Task A_caller_that_cancels_gets_a_cancellation_and_nothing_is_logged_as_a_failure()
    {
        using var host = new KohaTestHost(oauth: async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return KohaResponses.Token();
        });
        using var caller = new CancellationTokenSource();

        var waiting = host.Tokens.GetAsync(caller.Token);
        await Wait.UntilAsync(() => host.OAuth.Requests.Count == 1);
        await caller.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(Wait.Limit, TestContext.Current.CancellationToken));
        Assert.DoesNotContain(host.Logs.Entries, entry => entry.Level >= LogLevel.Warning);
    }
}
