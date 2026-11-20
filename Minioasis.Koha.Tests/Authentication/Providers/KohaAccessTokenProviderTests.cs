using Microsoft.Extensions.Time.Testing;
using Minioasis.Koha.Authentication.Exceptions;
using Minioasis.Koha.Authentication.Providers;
using Minioasis.Koha.Shared.Clients;
using Minioasis.Koha.Shared.Exceptions;
using Minioasis.Koha.Shared.Options;
using Minioasis.Koha.Tests.Shared.TestDoubles;
using System.Net;
using Xunit;

namespace Minioasis.Koha.Tests.Authentication.Providers;

public sealed class KohaAccessTokenProviderTests
{
    // Reserved form characters, so the test sees that the values are encoded, not only that they are present.
    private const string ClientId = "client id&=+";
    private const string ClientSecret = "client secret&=+/?%";

    // Plan, "Agent's discretion": the refresh margin is 30 seconds.
    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(30);
    private static readonly DateTimeOffset Start = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    // R15, EX-006, EX-007: one form POST to <base>/oauth/token through the OAuth client, with the three fields and no
    // Authorization header. The base comes from the options, with or without a trailing slash; the generated
    // default ("/api/v1", relative) would fail or go elsewhere.
    [Theory]
    [InlineData("https://koha.test/koha/api/v1")]
    [InlineData("https://koha.test/koha/api/v1/")]
    public async Task Sends_one_client_credentials_form_post(string baseUrl)
    {
        var handler = new StubHttpMessageHandler(() => Token("token-1", 3600));
        var factory = new StubHttpClientFactory(handler);
        var provider = new KohaAccessTokenProvider(factory, Options(new Uri(baseUrl)), new FakeTimeProvider(Start));

        var token = await provider.GetTokenAsync(TestCancellation).WaitAsync(Wait, TestCancellation);

        Assert.Equal("token-1", token);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://koha.test/koha/api/v1/oauth/token", request.Uri?.AbsoluteUri);
        Assert.Equal("application/x-www-form-urlencoded", request.ContentType);
        Assert.Equal(
            [
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("client_secret", ClientSecret),
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
            ],
            ParseForm(request.Body).OrderBy(field => field.Key, StringComparer.Ordinal));
        Assert.DoesNotContain(
            request.Headers,
            header => string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase));
        Assert.Equal([KohaClientNames.OAuth], factory.CreatedNames);
    }

    // R16: a 200 reply is accepted only with a non-blank access_token, an integer expires_in from 1 to 2147483647,
    // and token_type Bearer in any case. Anything else is an authentication failure that carries status 200.
    [Theory]
    [InlineData("""{"access_token":"tok","token_type":"Bearer","expires_in":3600}""", true)]
    [InlineData("""{"access_token":"tok","token_type":"bearer","expires_in":3600}""", true)]
    [InlineData("""{"access_token":"tok","token_type":"BEARER","expires_in":3600}""", true)]
    [InlineData("""{"access_token":"tok","token_type":"Bearer","expires_in":1}""", true)]
    [InlineData("""{"access_token":"tok","token_type":"Bearer","expires_in":2147483647}""", true)]
    [InlineData("""{"token_type":"Bearer","expires_in":3600}""", false)]
    [InlineData("""{"access_token":null,"token_type":"Bearer","expires_in":3600}""", false)]
    [InlineData("""{"access_token":"","token_type":"Bearer","expires_in":3600}""", false)]
    [InlineData("""{"access_token":" \t ","token_type":"Bearer","expires_in":3600}""", false)]
    [InlineData("""{"access_token":"tok","token_type":"Bearer"}""", false)]
    [InlineData("""{"access_token":"tok","token_type":"Bearer","expires_in":0}""", false)]
    [InlineData("""{"access_token":"tok","token_type":"Bearer","expires_in":-1}""", false)]
    [InlineData("""{"access_token":"tok","token_type":"Bearer","expires_in":"3600"}""", false)]
    [InlineData("""{"access_token":"tok","token_type":"Bearer","expires_in":3600.5}""", false)]
    [InlineData("""{"access_token":"tok","token_type":"Bearer","expires_in":2147483648}""", false)]
    [InlineData("""{"access_token":"tok","token_type":"Bearer","expires_in":null}""", false)]
    [InlineData("""{"access_token":"tok","expires_in":3600}""", false)]
    [InlineData("""{"access_token":"tok","token_type":"mac","expires_in":3600}""", false)]
    [InlineData("""{"access_token":"tok","token_type":"Bearer ","expires_in":3600}""", false)]
    [InlineData("not json", false)]
    [InlineData("", false)]
    [InlineData("null", false)]
    [InlineData("[]", false)]
    public async Task Accepts_or_rejects_token_reply(string body, bool accepted)
    {
        var handler = new StubHttpMessageHandler(() => StubHttpMessageHandler.Reply(HttpStatusCode.OK, body));
        var provider = Provider(handler, new FakeTimeProvider(Start));

        if (accepted)
        {
            Assert.Equal("tok", await provider.GetTokenAsync(TestCancellation).WaitAsync(Wait, TestCancellation));
        }
        else
        {
            var exception = await Assert.ThrowsAsync<KohaAuthenticationException>(
                () => provider.GetTokenAsync(TestCancellation).WaitAsync(Wait, TestCancellation));
            Assert.Equal(200, exception.KohaStatus);
            Assert.Null(exception.InnerException);
        }
    }

    // R20, token step: Koha could not serve the request (429, 500 to 599) is "unavailable"; any other status than
    // 200 is "authentication failed". Both carry Koha's status and no inner exception (R23).
    [Theory]
    [InlineData(201, false)]
    [InlineData(302, false)]
    [InlineData(400, false)]
    [InlineData(401, false)]
    [InlineData(403, false)]
    [InlineData(404, false)]
    [InlineData(428, false)]
    [InlineData(429, true)]
    [InlineData(499, false)]
    [InlineData(500, true)]
    [InlineData(503, true)]
    [InlineData(599, true)]
    [InlineData(600, false)]
    public async Task Refused_token_request_is_classified_by_status(int status, bool unavailable)
    {
        var handler = new StubHttpMessageHandler(
            () => StubHttpMessageHandler.Reply((HttpStatusCode)status, """{"error":"refused"}"""));
        var provider = Provider(handler, new FakeTimeProvider(Start));

        var exception = await Record.ExceptionAsync(
            () => provider.GetTokenAsync(TestCancellation).WaitAsync(Wait, TestCancellation));

        if (unavailable)
        {
            var failure = Assert.IsType<KohaUnavailableException>(exception);
            Assert.Equal(status, failure.KohaStatus);
            Assert.Null(failure.InnerException);
        }
        else
        {
            var failure = Assert.IsType<KohaAuthenticationException>(exception);
            Assert.Equal(status, failure.KohaStatus);
            Assert.Null(failure.InnerException);
        }
    }

    // R20, token step: a network error (HttpRequestException, or an IOException while the body is read) is
    // "unavailable", without a status; the network error is kept as the inner exception.
    [Theory]
    [InlineData("connect")]
    [InlineData("body")]
    public async Task Network_error_is_unavailable(string failure)
    {
        Exception? thrown = null;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            if (failure == "connect")
            {
                thrown = new HttpRequestException("The test connection failed.");
                throw thrown;
            }

            return Task.FromResult(StubHttpMessageHandler.BrokenBodyReply(HttpStatusCode.OK));
        });
        var provider = Provider(handler, new FakeTimeProvider(Start));

        var exception = await Assert.ThrowsAsync<KohaUnavailableException>(
            () => provider.GetTokenAsync(TestCancellation).WaitAsync(Wait, TestCancellation));

        Assert.Null(exception.KohaStatus);
        if (thrown is not null)
        {
            Assert.Same(thrown, exception.InnerException);
        }
        else
        {
            Assert.IsType<IOException>(exception.InnerException);
        }
    }

    // R17: a cached token is reused until its expiry minus the margin, without a new request.
    [Fact]
    public async Task Reuses_until_margin()
    {
        var time = new FakeTimeProvider(Start);
        var handler = NumberedTokens(3600);
        var provider = Provider(handler, time);

        Assert.Equal("token-1", await GetAsync(provider));
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("token-1", await GetAsync(provider));
        time.SetUtcNow(Start + TimeSpan.FromSeconds(3600) - Margin - TimeSpan.FromSeconds(1));
        Assert.Equal("token-1", await GetAsync(provider));

        Assert.Single(handler.Requests);
    }

    // R17: at the refresh moment the next call requests a new token, which is then cached in turn.
    [Fact]
    public async Task Refreshes_after_margin()
    {
        var time = new FakeTimeProvider(Start);
        var handler = NumberedTokens(3600);
        var provider = Provider(handler, time);

        Assert.Equal("token-1", await GetAsync(provider));
        time.SetUtcNow(Start + TimeSpan.FromSeconds(3600) - Margin);
        Assert.Equal("token-2", await GetAsync(provider));
        Assert.Equal("token-2", await GetAsync(provider));

        Assert.Equal(2, handler.Requests.Count);
    }

    // R17: a token whose lifetime is not longer than the margin is used for the call that requested it and is not
    // cached.
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public async Task Short_lived_token_is_used_once_and_not_cached(int lifetimeSeconds)
    {
        var handler = NumberedTokens(lifetimeSeconds);
        var provider = Provider(handler, new FakeTimeProvider(Start));

        Assert.Equal("token-1", await GetAsync(provider));
        Assert.Equal("token-2", await GetAsync(provider));

        Assert.Equal(2, handler.Requests.Count);
    }

    // R17, plan "Tests": a lifetime of exactly the margin is not cached; one second more is cached until one tick
    // before expiry minus the margin, and refreshed exactly at it.
    [Fact]
    public async Task Margin_boundary()
    {
        var time = new FakeTimeProvider(Start);
        var notCached = NumberedTokens(30);
        var notCachedProvider = Provider(notCached, time);

        Assert.Equal("token-1", await GetAsync(notCachedProvider));
        Assert.Equal("token-2", await GetAsync(notCachedProvider));

        var cached = NumberedTokens(31);
        var cachedProvider = Provider(cached, time);
        var refreshAt = Start + TimeSpan.FromSeconds(31) - Margin;

        Assert.Equal("token-1", await GetAsync(cachedProvider));
        time.SetUtcNow(refreshAt - TimeSpan.FromTicks(1));
        Assert.Equal("token-1", await GetAsync(cachedProvider));
        Assert.Single(cached.Requests);
        time.SetUtcNow(refreshAt);
        Assert.Equal("token-2", await GetAsync(cachedProvider));
        Assert.Equal(2, cached.Requests.Count);
    }

    // R18: ten callers that need a token at the same moment share one request and all receive its token.
    [Fact]
    public async Task Ten_simultaneous_callers_share_one_request()
    {
        var release = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler((_, _) => release.Task);
        var provider = Provider(handler, new FakeTimeProvider(Start));

        var calls = Enumerable.Range(0, 10).Select(_ => provider.GetTokenAsync(TestCancellation)).ToList();
        await handler.WaitForRequestsAsync(1);
        Assert.All(calls, call => Assert.False(call.IsCompleted));
        release.SetResult(Token("shared", 3600));
        var tokens = await Task.WhenAll(calls).WaitAsync(Wait, TestCancellation);

        Assert.All(tokens, token => Assert.Equal("shared", token));
        Assert.Single(handler.Requests);
        Assert.Equal("shared", await GetAsync(provider));
        Assert.Single(handler.Requests);
    }

    // R18: a failed shared request reaches every waiting caller, and it is not cached: the next call sends a new one.
    [Fact]
    public async Task Ten_simultaneous_callers_share_one_failure_and_the_next_call_sends_a_new_request()
    {
        var release = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler(
            (request, _) => request.Index == 0 ? release.Task : Task.FromResult(Token("next", 3600)));
        var provider = Provider(handler, new FakeTimeProvider(Start));

        var calls = Enumerable.Range(0, 10).Select(_ => provider.GetTokenAsync(TestCancellation)).ToList();
        await handler.WaitForRequestsAsync(1);
        release.SetResult(StubHttpMessageHandler.Reply(HttpStatusCode.BadRequest, """{"error":"invalid_client"}"""));

        foreach (var call in calls)
        {
            var exception = await Assert.ThrowsAsync<KohaAuthenticationException>(
                () => call.WaitAsync(Wait, TestCancellation));
            Assert.Equal(400, exception.KohaStatus);
        }

        Assert.Single(handler.Requests);
        Assert.Equal("next", await GetAsync(provider));
        Assert.Equal(2, handler.Requests.Count);
    }

    // R18, R22: a caller that cancels stops waiting at once, with its own cancellation; the shared request goes on,
    // uncancelled, and the other nine receive its token. The caller that cancels is the one that started the request.
    [Fact]
    public async Task One_caller_cancelling_leaves_the_others()
    {
        var release = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler((_, _) => release.Task);
        var provider = Provider(handler, new FakeTimeProvider(Start));
        var callers = Enumerable.Range(0, 10).Select(_ => new CancellationTokenSource()).ToList();

        var calls = callers.Select(caller => provider.GetTokenAsync(caller.Token)).ToList();
        await handler.WaitForRequestsAsync(1);
        await callers[0].CancelAsync();

        var cancelled = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => calls[0].WaitAsync(Wait, TestCancellation));
        Assert.Equal(callers[0].Token, cancelled.CancellationToken);
        Assert.False(handler.Requests[0].CancellationToken.IsCancellationRequested);

        release.SetResult(Token("shared", 3600));
        var others = calls.Where((_, index) => index != 0).ToList();
        var tokens = await Task.WhenAll(others).WaitAsync(Wait, TestCancellation);

        Assert.All(tokens, token => Assert.Equal("shared", token));
        Assert.Single(handler.Requests);
        callers.ForEach(caller => caller.Dispose());
    }

    // R18: when every waiting caller has cancelled, the shared request is cancelled, and the next call does not join
    // it but sends a new request, even while the abandoned one has not ended yet. The abandoned request's result, if
    // it still arrives in full, is never cached. Covered with a fake Koha that honours the cancellation and with one
    // whose reply had already arrived.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task All_callers_cancelling_cancels_and_caches_nothing(bool honoursCancellation)
    {
        // No RunContinuationsAsynchronously: releasing the abandoned reply runs its processing on the test thread, so
        // that a wrongly cached result would be in place before the next call.
        var release = new TaskCompletionSource<HttpResponseMessage>();
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.Index == 0)
            {
                var response = honoursCancellation
                    ? await release.Task.WaitAsync(cancellationToken)
                    : await release.Task;
                return response;
            }

            return Token($"token-{request.Index + 1}", 3600);
        });
        var provider = Provider(handler, new FakeTimeProvider(Start));
        var callers = Enumerable.Range(0, 10).Select(_ => new CancellationTokenSource()).ToList();

        var calls = callers.Select(caller => provider.GetTokenAsync(caller.Token)).ToList();
        await handler.WaitForRequestsAsync(1);
        foreach (var (caller, call) in callers.Zip(calls))
        {
            await caller.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call.WaitAsync(Wait, TestCancellation));
        }

        Assert.True(handler.Requests[0].CancellationToken.IsCancellationRequested);
        Assert.Equal("token-2", await GetAsync(provider));
        Assert.Equal(2, handler.Requests.Count);

        release.SetResult(StubHttpMessageHandler.ArrivedReply(
            HttpStatusCode.OK,
            """{"access_token":"abandoned","token_type":"Bearer","expires_in":3600}"""));

        Assert.Equal("token-2", await GetAsync(provider));
        Assert.Equal(2, handler.Requests.Count);
        callers.ForEach(caller => caller.Dispose());
    }

    // R18: an abandoned request that fails only after a new request has started does not disturb the new one: the
    // new request's token reaches its caller and is cached.
    [Fact]
    public async Task Abandoned_request_failing_later_leaves_the_new_request_alone()
    {
        // No RunContinuationsAsynchronously on the abandoned reply: its failure is processed on the test thread, before
        // the new request is released.
        var abandoned = new TaskCompletionSource<HttpResponseMessage>();
        var next = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler((request, _) => request.Index switch
        {
            0 => abandoned.Task,
            1 => next.Task,
            _ => Task.FromResult(Token($"token-{request.Index + 1}", 3600)),
        });
        var provider = Provider(handler, new FakeTimeProvider(Start));
        using var caller = new CancellationTokenSource();

        var call = provider.GetTokenAsync(caller.Token);
        await handler.WaitForRequestsAsync(1);
        await caller.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call.WaitAsync(Wait, TestCancellation));

        var nextCall = provider.GetTokenAsync(TestCancellation);
        await handler.WaitForRequestsAsync(2);
        abandoned.SetException(new HttpRequestException("The abandoned test request failed."));
        next.SetResult(Token("token-2", 3600));

        Assert.Equal("token-2", await nextCall.WaitAsync(Wait, TestCancellation));
        Assert.Equal("token-2", await GetAsync(provider));
        Assert.Equal(2, handler.Requests.Count);
    }

    // R18, PR 4 review N1: the last waiter's cancellation reaches Leave only after its request R has already failed
    // and a caller C has started a newer request R2. Leave must leave R2 alone, so that a new caller D joins R2 and
    // exactly two requests are sent. The order is forced through the token's callbacks, which run in the reverse
    // order of registration: the test's callback, registered after the call, fails R and starts R2 before the
    // provider's own callback ends W's wait. W's wait may still end with R's failure first, from the thread pool; such
    // a run does not reach the race and is skipped. Measured on 4 cores, 5 to 45 % of runs reach it once warm, so the
    // loop stops after 20 that did, and fails if none of 2000 did.
    [Fact]
    public async Task Last_waiter_leaving_after_its_request_ended_leaves_the_newer_request_alone()
    {
        const int MaxRuns = 2000;
        const int Enough = 20;
        var reached = 0;
        for (var run = 0; run < MaxRuns && reached < Enough; run++)
        {
            if (await LeaveAfterTheRequestEndedAsync())
            {
                reached++;
            }
        }

        Assert.True(reached > 0, $"None of the {MaxRuns} runs reached the race.");
    }

    // N3: a caller whose token is already cancelled stops at once and sends no token request, even when none is
    // cached.
    [Fact]
    public async Task Already_cancelled_caller_sends_no_token_request()
    {
        var handler = NumberedTokens(3600);
        var provider = Provider(handler, new FakeTimeProvider(Start));
        using var caller = new CancellationTokenSource();
        await caller.CancelAsync();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GetTokenAsync(caller.Token).WaitAsync(Wait, TestCancellation));

        Assert.Equal(caller.Token, exception.CancellationToken);
        Assert.Empty(handler.Requests);
        Assert.Equal("token-1", await GetAsync(provider));
    }

    // N6, R23, ER-008: the provider's own types that hold the access token cannot write it as text. A record's
    // generated ToString would.
    [Fact]
    public void Nested_types_do_not_write_their_values_as_text()
    {
        var nested = typeof(KohaAccessTokenProvider).GetNestedTypes(System.Reflection.BindingFlags.NonPublic);

        Assert.NotEmpty(nested);
        Assert.All(nested, type => Assert.Null(type.GetMethod(
            nameof(ToString),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.DeclaredOnly,
            Type.EmptyTypes)));
    }

    // R19 (provider part), EX-009: Invalidate discards the cached token it names, so the next call requests a new one.
    [Fact]
    public async Task Invalidate_discards_the_token_it_names()
    {
        var handler = NumberedTokens(3600);
        var provider = Provider(handler, new FakeTimeProvider(Start));

        Assert.Equal("token-1", await GetAsync(provider));
        provider.Invalidate("token-1");

        Assert.Equal("token-2", await GetAsync(provider));
        Assert.Equal(2, handler.Requests.Count);
    }

    // R19 (provider part): a token that is not the cached one is ignored, so a newer token that another call already
    // obtained is kept.
    [Fact]
    public async Task Invalidate_keeps_a_newer_token()
    {
        var handler = NumberedTokens(3600);
        var provider = Provider(handler, new FakeTimeProvider(Start));

        Assert.Equal("token-1", await GetAsync(provider));
        provider.Invalidate("token-1");
        Assert.Equal("token-2", await GetAsync(provider));

        provider.Invalidate("token-1");
        provider.Invalidate("token-unknown");

        Assert.Equal("token-2", await GetAsync(provider));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public void Null_arguments_throw()
    {
        var factory = new StubHttpClientFactory(NumberedTokens(3600));
        var options = Options(new Uri("https://koha.test/api/v1/"));
        var time = new FakeTimeProvider(Start);

        Assert.Equal(
            "httpClientFactory",
            Assert.Throws<ArgumentNullException>(() => new KohaAccessTokenProvider(null!, options, time)).ParamName);
        Assert.Equal(
            "options",
            Assert.Throws<ArgumentNullException>(() => new KohaAccessTokenProvider(factory, null!, time)).ParamName);
        Assert.Equal(
            "timeProvider",
            Assert.Throws<ArgumentNullException>(() => new KohaAccessTokenProvider(factory, options, null!)).ParamName);
    }

    // One run of the N1 race. Returns false when W's wait ended with R's failure instead of its cancellation, so the
    // run did not reach the race; otherwise asserts that the newer request R2 was kept.
    private static async Task<bool> LeaveAfterTheRequestEndedAsync()
    {
        // No RunContinuationsAsynchronously on R's reply: its failure is processed inside the test's callback.
        var first = new TaskCompletionSource<HttpResponseMessage>();
        var second = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler((request, _) => request.Index switch
        {
            0 => first.Task,
            1 => second.Task,
            _ => Task.FromResult(Token($"token-{request.Index + 1}", 3600)),
        });
        var provider = Provider(handler, new FakeTimeProvider(Start));
        using var waiter = new CancellationTokenSource();

        var w = provider.GetTokenAsync(waiter.Token);
        await handler.WaitForRequestsAsync(1);
        Task<string>? c = null;
        var requestsAfterC = 0;
        using (waiter.Token.Register(() =>
        {
            first.SetException(new HttpRequestException("The test request R failed."));
            c = provider.GetTokenAsync(TestCancellation);
            requestsAfterC = handler.Requests.Count;
        }))
        {
            await waiter.CancelAsync();
        }

        var wOutcome = await Record.ExceptionAsync(() => w.WaitAsync(Wait, TestCancellation));
        if (wOutcome is not OperationCanceledException || requestsAfterC != 2)
        {
            // W received R's failure first, or R did not fail inside the callback: the race was not reached.
            second.TrySetResult(Token("token-2", 3600));
            await Record.ExceptionAsync(() => c!.WaitAsync(Wait, TestCancellation));
            return false;
        }

        var d = provider.GetTokenAsync(TestCancellation);
        second.SetResult(Token("token-2", 3600));

        Assert.Equal("token-2", await c!.WaitAsync(Wait, TestCancellation));
        Assert.Equal("token-2", await d.WaitAsync(Wait, TestCancellation));
        Assert.Equal(2, handler.Requests.Count);
        return true;
    }

    private static KohaAccessTokenProvider Provider(HttpMessageHandler handler, TimeProvider time) =>
        new(new StubHttpClientFactory(handler), Options(new Uri("https://koha.test/api/v1/")), time);

    private static KohaOptions Options(Uri baseUrl) => new()
    {
        BaseUrl = baseUrl,
        ClientId = ClientId,
        ClientSecret = ClientSecret,
        Deadline = TimeSpan.FromSeconds(10),
    };

    private static Task<string> GetAsync(KohaAccessTokenProvider provider) =>
        provider.GetTokenAsync(TestCancellation).WaitAsync(Wait, TestCancellation);

    // Replies "token-1", "token-2", ... in the order the requests arrive.
    private static StubHttpMessageHandler NumberedTokens(int lifetimeSeconds) =>
        new((request, _) => Task.FromResult(Token($"token-{request.Index + 1}", lifetimeSeconds)));

    private static HttpResponseMessage Token(string accessToken, int lifetimeSeconds) =>
        StubHttpMessageHandler.Reply(
            HttpStatusCode.OK,
            $$"""{"access_token":"{{accessToken}}","token_type":"Bearer","expires_in":{{lifetimeSeconds}}}""");

    private static List<KeyValuePair<string, string>> ParseForm(string? body) =>
    [
        .. (body ?? string.Empty)
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(field => field.Split('=', 2))
            .Select(pair => new KeyValuePair<string, string>(Decode(pair[0]), Decode(pair[1]))),
    ];

    private static string Decode(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));
}
