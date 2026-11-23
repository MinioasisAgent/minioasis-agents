using Minioasis.Koha.Authentication.Exceptions;
using Minioasis.Koha.Authentication.Handlers;
using Minioasis.Koha.Tests.Shared.TestDoubles;
using System.Net;
using Xunit;

namespace Minioasis.Koha.Tests.Authentication.Handlers;

public sealed class KohaBearerHandlerTests
{
    private int issued;

    [Fact]
    public async Task Attaches_the_access_token_as_a_bearer_token()
    {
        using var host = TokenHost();
        var api = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        using var response = await SendAsync(host, api, HttpMethod.Get);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var request = Assert.Single(api.Requests);
        Assert.Equal("Bearer", request.Authorization!.Scheme);
        Assert.Equal("token-1", request.Authorization.Parameter);
    }

    [Fact]
    public async Task After_a_401_on_a_get_discards_the_token_gets_a_new_one_and_sends_the_request_once_more()
    {
        using var host = TokenHost();
        var answers = new Queue<HttpResponseMessage>(
        [
            KohaResponses.Json(HttpStatusCode.Unauthorized, """{"error":"Token expired"}""", KohaResponses.Utf8WithoutHyphen),
            new HttpResponseMessage(HttpStatusCode.OK),
        ]);
        var api = new StubHttpMessageHandler(_ => answers.Dequeue());

        using var response = await SendAsync(host, api, HttpMethod.Get);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, host.OAuth.Requests.Count);
        Assert.Collection(
            api.Requests,
            first => Assert.Equal("token-1", first.Authorization!.Parameter),
            retry =>
            {
                Assert.Equal("token-2", retry.Authorization!.Parameter);
                Assert.Equal(HttpMethod.Get, retry.Method);
                Assert.Equal(new Uri("https://koha.test/api/v1/items/12"), retry.Uri);
                Assert.Equal("application/json", Assert.Single(retry.Headers.Accept).MediaType);
            });
        Assert.Equal("token-2", await host.Tokens.GetAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Returns_the_second_401_without_trying_again()
    {
        using var host = TokenHost();
        var api = new StubHttpMessageHandler(_ =>
            KohaResponses.Json(HttpStatusCode.Unauthorized, """{"error":"Invalid token"}""", KohaResponses.Utf8WithoutHyphen));

        using var response = await SendAsync(host, api, HttpMethod.Get);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(2, api.Requests.Count);
        Assert.Equal(2, host.OAuth.Requests.Count);
    }

    public static TheoryData<string> OtherMethods => new() { "POST", "PUT", "PATCH", "DELETE", "HEAD" };

    [Theory]
    [MemberData(nameof(OtherMethods))]
    public async Task Never_sends_a_request_other_than_get_again_after_a_401(string method)
    {
        using var host = TokenHost();
        var api = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        using var response = await SendAsync(host, api, new HttpMethod(method));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(api.Requests);
        Assert.Single(host.OAuth.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Sends_a_get_only_once_after_any_status_other_than_401(HttpStatusCode status)
    {
        using var host = TokenHost();
        var api = new StubHttpMessageHandler(_ => new HttpResponseMessage(status));

        using var response = await SendAsync(host, api, HttpMethod.Get);

        Assert.Equal(status, response.StatusCode);
        Assert.Single(api.Requests);
    }

    [Fact]
    public async Task Does_not_send_the_request_when_no_token_can_be_obtained()
    {
        using var host = new KohaTestHost(oauth: (_, _) =>
            Task.FromResult(KohaResponses.Json(HttpStatusCode.Forbidden, """{"error":"access_denied"}""")));
        var api = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Assert.ThrowsAsync<KohaAuthenticationException>(() => SendAsync(host, api, HttpMethod.Get));
        Assert.Empty(api.Requests);
    }

    [Fact]
    public async Task Reports_a_token_wait_that_is_cancelled_as_an_authentication_failure()
    {
        using var host = new KohaTestHost(oauth: async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return KohaResponses.Token();
        });
        var api = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var deadline = new CancellationTokenSource();

        var sending = SendAsync(host, api, HttpMethod.Get, deadline.Token);
        await Wait.UntilAsync(() => host.OAuth.Requests.Count == 1);
        await deadline.CancelAsync();

        await Assert.ThrowsAsync<KohaAuthenticationException>(
            () => sending.WaitAsync(Wait.Limit, TestContext.Current.CancellationToken));
        Assert.Empty(api.Requests);
    }

    private KohaTestHost TokenHost() =>
        new(oauth: (_, _) => Task.FromResult(KohaResponses.Token(token: $"token-{Interlocked.Increment(ref issued)}")));

    private static async Task<HttpResponseMessage> SendAsync(
        KohaTestHost host,
        StubHttpMessageHandler api,
        HttpMethod method,
        CancellationToken? cancellationToken = null)
    {
        using var invoker = new HttpMessageInvoker(new KohaBearerHandler(host.Tokens) { InnerHandler = api });
        using var request = new HttpRequestMessage(method, "https://koha.test/api/v1/items/12");
        request.Headers.Accept.ParseAdd("application/json");
        if (method != HttpMethod.Get && method != HttpMethod.Head)
        {
            request.Content = new StringContent("{}");
        }

        return await invoker.SendAsync(request, cancellationToken ?? TestContext.Current.CancellationToken);
    }
}
