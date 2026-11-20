using Microsoft.Extensions.Time.Testing;
using Minioasis.Koha.Authentication.Handlers;
using Minioasis.Koha.Authentication.Providers;
using Minioasis.Koha.Shared.Options;
using Minioasis.Koha.Tests.Shared.TestDoubles;
using System.Net;
using Xunit;

namespace Minioasis.Koha.Tests.Authentication.Handlers;

/// <summary>
/// Spec 001, R19, <c>EX-009</c>, <c>EX-010</c>: the bearer handler attaches the token, retries a <c>GET</c> once
/// after a 401 with a new token, never sends any other method again, and discards the token that got the 401 only
/// while it is still the cached one. The handler is tested directly, over a real token provider whose token
/// requests go to a fake Koha (<c>TS-002</c>, <c>TS-004</c>).
/// </summary>
public sealed class KohaBearerHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);
    private static readonly Uri Address = new("https://koha.test/api/v1/patrons?q=1");

    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    // R19: every protected call carries Authorization: Bearer <token>, and the cached token is reused.
    [Fact]
    public async Task Attaches_the_bearer_token()
    {
        var tokens = NumberedTokens();
        var api = new StubHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.OK));
        using var invoker = Invoker(tokens, api);

        using var first = await SendAsync(invoker, HttpMethod.Get);
        using var second = await SendAsync(invoker, HttpMethod.Get);

        Assert.Equal(["Bearer token-1", "Bearer token-1"], api.Requests.Select(Authorization));
        Assert.Single(tokens.Requests);
    }

    // R19, EX-009: a GET answered 401 is sent once more with a new token, as a new request with the same method,
    // address, and other headers; the reply to the second attempt is the result.
    [Fact]
    public async Task Get_with_401_then_200_is_retried_once_with_a_new_token()
    {
        var tokens = NumberedTokens();
        var api = new StubHttpMessageHandler((request, _) => Task.FromResult(new HttpResponseMessage(
            Authorization(request) == "Bearer token-1" ? HttpStatusCode.Unauthorized : HttpStatusCode.OK)));
        using var invoker = Invoker(tokens, api);

        using var response = await SendAsync(invoker, HttpMethod.Get);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["Bearer token-1", "Bearer token-2"], api.Requests.Select(Authorization));
        Assert.All(api.Requests, request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(Address, request.Uri);
            Assert.Equal("application/json", Header(request, "Accept"));
            Assert.Equal("kept", Header(request, "X-Test"));
        });
        Assert.Equal(2, tokens.Requests.Count);

        // The new token that succeeded is kept for the next call.
        using var next = await SendAsync(invoker, HttpMethod.Get);
        Assert.Equal("Bearer token-2", Authorization(api.Requests[2]));
        Assert.Equal(2, tokens.Requests.Count);
    }

    // R19, EX-009: a GET answered 401 twice is sent exactly twice; the second 401 is final. The token that got the
    // second 401 is discarded like the first, so the next call uses a new token.
    [Fact]
    public async Task Get_with_401_then_401_is_final()
    {
        var tokens = NumberedTokens();
        var api = new StubHttpMessageHandler((request, _) => Task.FromResult(new HttpResponseMessage(
            request.Index < 2 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK)));
        using var invoker = Invoker(tokens, api);

        using var response = await SendAsync(invoker, HttpMethod.Get);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(["Bearer token-1", "Bearer token-2"], api.Requests.Select(Authorization));
        Assert.Equal(2, tokens.Requests.Count);

        using var next = await SendAsync(invoker, HttpMethod.Get);

        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        Assert.Equal("Bearer token-3", Authorization(api.Requests[2]));
        Assert.Equal(3, tokens.Requests.Count);
    }

    // R19: when another call has already replaced the token that got the 401, that newer token is kept and used for
    // the retry; no third token request is sent.
    [Fact]
    public async Task Get_with_401_after_another_call_refreshed_keeps_the_newer_token()
    {
        var tokens = NumberedTokens();
        var firstReply = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new StubHttpMessageHandler((request, _) => request.Index == 0
            ? firstReply.Task
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var provider = Provider(tokens);
        using var invoker = Invoker(provider, api);

        var call = SendAsync(invoker, HttpMethod.Get);
        await api.WaitForRequestsAsync(1);
        provider.Invalidate("token-1");
        Assert.Equal("token-2", await provider.GetTokenAsync(TestCancellation).WaitAsync(Wait, TestCancellation));
        firstReply.SetResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var response = await call;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["Bearer token-1", "Bearer token-2"], api.Requests.Select(Authorization));
        Assert.Equal(2, tokens.Requests.Count);
    }

    // R19, EX-010: a request with any other method than GET answered 401 is never sent again, and the 401 is the
    // result; the token that got it is discarded, so the next call uses a new token.
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("HEAD")]
    public async Task Other_method_with_401_is_not_sent_again_and_the_token_is_discarded(string method)
    {
        var tokens = NumberedTokens();
        var api = new StubHttpMessageHandler((request, _) => Task.FromResult(new HttpResponseMessage(
            request.Index == 0 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK)));
        using var invoker = Invoker(tokens, api);

        using var response = await SendAsync(invoker, new HttpMethod(method));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(api.Requests);
        Assert.Single(tokens.Requests);

        using var next = await SendAsync(invoker, HttpMethod.Get);

        Assert.Equal("Bearer token-2", Authorization(api.Requests[1]));
        Assert.Equal(2, tokens.Requests.Count);
    }

    // R19: only a 401 leads to a retry. A GET answered 500 is not sent again, and its token is kept.
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Get_with_another_failure_is_not_retried(HttpStatusCode status)
    {
        var tokens = NumberedTokens();
        var api = new StubHttpMessageHandler((request, _) => Task.FromResult(new HttpResponseMessage(
            request.Index == 0 ? status : HttpStatusCode.OK)));
        using var invoker = Invoker(tokens, api);

        using var response = await SendAsync(invoker, HttpMethod.Get);
        using var next = await SendAsync(invoker, HttpMethod.Get);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(["Bearer token-1", "Bearer token-1"], api.Requests.Select(Authorization));
        Assert.Single(tokens.Requests);
    }

    // R18, R19: ten GETs that all get a 401 on the same token share one new token request. The new token request is
    // held until it has arrived, so that the callers that reach the provider meanwhile wait for it; any that come
    // later find the new token cached, which a 401 on the old token does not discard.
    [Fact]
    public async Task Simultaneous_401s_share_one_token_request()
    {
        var secondToken = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokens = new StubHttpMessageHandler((request, _) => request.Index == 0
            ? Task.FromResult(Token("token-1"))
            : request.Index == 1
                ? secondToken.Task
                : Task.FromResult(Token($"token-{request.Index + 1}")));
        var firstReplies = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new StubHttpMessageHandler(async (request, _) =>
        {
            if (Authorization(request) != "Bearer token-1")
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            await firstReplies.Task;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });
        var provider = Provider(tokens);
        using var invoker = Invoker(provider, api);
        Assert.Equal("token-1", await provider.GetTokenAsync(TestCancellation).WaitAsync(Wait, TestCancellation));

        var calls = Enumerable.Range(0, 10).Select(_ => SendAsync(invoker, HttpMethod.Get)).ToList();
        await api.WaitForRequestsAsync(10);
        firstReplies.SetResult();
        await tokens.WaitForRequestsAsync(2);
        secondToken.SetResult(Token("token-2"));
        var responses = await Task.WhenAll(calls).WaitAsync(Wait, TestCancellation);

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(2, tokens.Requests.Count);
        Assert.Equal(20, api.Requests.Count);
        Assert.Equal(10, api.Requests.Count(request => Authorization(request) == "Bearer token-2"));
        Array.ForEach(responses, response => response.Dispose());
    }

    [Fact]
    public void Null_token_provider_throws()
    {
        Assert.Equal(
            "tokens",
            Assert.Throws<ArgumentNullException>(() => new KohaBearerHandler(null!)).ParamName);
    }

    private static HttpMessageInvoker Invoker(StubHttpMessageHandler tokens, HttpMessageHandler api) =>
        Invoker(Provider(tokens), api);

    private static HttpMessageInvoker Invoker(KohaAccessTokenProvider provider, HttpMessageHandler api) =>
        new(new KohaBearerHandler(provider) { InnerHandler = api });

    private static KohaAccessTokenProvider Provider(StubHttpMessageHandler tokens) =>
        new(new StubHttpClientFactory(tokens), Options(), new FakeTimeProvider(Start));

    private static Task<HttpResponseMessage> SendAsync(HttpMessageInvoker invoker, HttpMethod method)
    {
        var request = new HttpRequestMessage(method, Address);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Add("X-Test", "kept");
        return invoker.SendAsync(request, TestCancellation).WaitAsync(Wait, TestCancellation);
    }

    private static string? Authorization(StubHttpMessageHandler.RecordedRequest request) =>
        Header(request, "Authorization");

    private static string? Header(StubHttpMessageHandler.RecordedRequest request, string name) =>
        request.Headers
            .Where(header => string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
            .Select(header => header.Value)
            .SingleOrDefault();

    // Replies "token-1", "token-2", ... in the order the token requests arrive.
    private static StubHttpMessageHandler NumberedTokens() =>
        new((request, _) => Task.FromResult(Token($"token-{request.Index + 1}")));

    private static HttpResponseMessage Token(string accessToken) =>
        StubHttpMessageHandler.Reply(
            HttpStatusCode.OK,
            $$"""{"access_token":"{{accessToken}}","token_type":"Bearer","expires_in":3600}""");

    private static KohaOptions Options() => new()
    {
        BaseUrl = new Uri("https://koha.test/api/v1/"),
        ClientId = "dummy-client-id",
        ClientSecret = "dummy-client-secret",
        Deadline = TimeSpan.FromSeconds(10),
    };
}
