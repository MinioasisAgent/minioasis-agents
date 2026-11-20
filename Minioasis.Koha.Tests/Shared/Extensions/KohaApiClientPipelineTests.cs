using Minioasis.Application.Shared.Outcomes;
using Minioasis.Koha.Authentication.Exceptions;
using Minioasis.Koha.Shared.Exceptions;
using Minioasis.Koha.Shared.Mappers;
using Minioasis.Koha.Tests.Shared.TestDoubles;
using System.Net;
using Xunit;

namespace Minioasis.Koha.Tests.Shared.Extensions;

/// <summary>
/// Spec 001, R18 to R22, through the API client exactly as <c>AddKohaIntegration</c> builds it: the deadline handler,
/// then the bearer handler, the token provider on the OAuth client with its own deadline handler, and a fake Koha
/// behind each client. Time is a <see cref="Microsoft.Extensions.Time.Testing.FakeTimeProvider"/>; nothing waits
/// for real time (<c>TS-007</c>).
/// </summary>
public sealed class KohaApiClientPipelineTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);
    private static readonly Uri Address = new(KohaClientsHarness.BaseUrl + "patrons/7");

    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    // R15, R19: a protected call gets a token through the OAuth client and carries it; the reply's body is read.
    [Fact]
    public async Task Protected_call_carries_the_token()
    {
        var tokens = NumberedTokens();
        var api = new StubHttpMessageHandler(() => StubHttpMessageHandler.Reply(HttpStatusCode.OK, """{"id":7}"""));
        using var harness = new KohaClientsHarness(tokens, api);

        using var response = await GetAsync(harness.CreateApiClient(), TestCancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"id":7}""", await response.Content.ReadAsStringAsync(TestCancellation));
        Assert.Equal("Bearer token-1", Authorization(Assert.Single(api.Requests)));
        Assert.Equal(KohaClientsHarness.BaseUrl + "oauth/token", Assert.Single(tokens.Requests).Uri?.AbsoluteUri);
    }

    // R20, token step, through the API client: each row ends in the documented Koha exception with Koha's status (or
    // none when Koha did not reply), the shared mapper gives the row's outcome, and no protected request is sent
    // after the failing token step. The last row: the token step fails while getting the new token after a 401.
    [Theory]
    [InlineData("201", DependencyFailureOutcome.AuthenticationFailed, 201)]
    [InlineData("302", DependencyFailureOutcome.AuthenticationFailed, 302)]
    [InlineData("400", DependencyFailureOutcome.AuthenticationFailed, 400)]
    [InlineData("401", DependencyFailureOutcome.AuthenticationFailed, 401)]
    [InlineData("403", DependencyFailureOutcome.AuthenticationFailed, 403)]
    [InlineData("404", DependencyFailureOutcome.AuthenticationFailed, 404)]
    [InlineData("429", DependencyFailureOutcome.Unavailable, 429)]
    [InlineData("500", DependencyFailureOutcome.Unavailable, 500)]
    [InlineData("599", DependencyFailureOutcome.Unavailable, 599)]
    [InlineData("network", DependencyFailureOutcome.Unavailable, null)]
    [InlineData("deadline", DependencyFailureOutcome.Unavailable, null)]
    [InlineData("retry", DependencyFailureOutcome.AuthenticationFailed, 400)]
    public async Task Token_step_failures_by_row(string row, DependencyFailureOutcome expected, int? kohaStatus)
    {
        var tokens = new StubHttpMessageHandler(async (request, cancellationToken) => (row, request.Index) switch
        {
            ("network", _) => throw new HttpRequestException("The test connection failed."),
            ("deadline", _) => await Stall(cancellationToken),
            ("retry", 0) => Token("token-1"),
            ("retry", _) => StubHttpMessageHandler.Reply(HttpStatusCode.BadRequest, """{"error":"invalid_client"}"""),
            _ => StubHttpMessageHandler.Reply((HttpStatusCode)int.Parse(row), """{"error":"refused"}"""),
        });
        var api = new StubHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var harness = new KohaClientsHarness(tokens, api);

        var call = GetAsync(harness.CreateApiClient(), TestCancellation);
        if (row == "deadline")
        {
            await tokens.WaitForRequestsAsync(1);
            harness.Time.Advance(KohaClientsHarness.Deadline);
        }

        var exception = await Record.ExceptionAsync(() => call.WaitAsync(Wait, TestCancellation));

        Assert.Equal(expected, KohaProtocolFailureMapper.Map(exception!));
        Assert.Equal(kohaStatus, exception switch
        {
            KohaAuthenticationException authentication => authentication.KohaStatus,
            KohaUnavailableException unavailable => unavailable.KohaStatus,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected exception: {exception?.GetType()}"),
        });
        Assert.Equal(row == "retry" ? 1 : 0, api.Requests.Count);
    }

    // R18: the shared token request has its own deadline from the moment it is sent. Caller A starts it; caller B
    // joins half a deadline later. When the shared request's deadline expires, B receives "unavailable" before its
    // own deadline, and the token request is cancelled.
    [Fact]
    public async Task Shared_token_request_expires_on_its_own_deadline()
    {
        var tokens = new StubHttpMessageHandler(async (_, cancellationToken) => await Stall(cancellationToken));
        var api = new StubHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.OK));
        using var harness = new KohaClientsHarness(tokens, api);
        var client = harness.CreateApiClient();

        var a = GetAsync(client, TestCancellation);
        await tokens.WaitForRequestsAsync(1);
        harness.Time.Advance(KohaClientsHarness.Deadline / 2);
        var b = GetAsync(client, TestCancellation);
        harness.Time.Advance(KohaClientsHarness.Deadline / 2);

        var exception = await Assert.ThrowsAsync<KohaUnavailableException>(() => b.WaitAsync(Wait, TestCancellation));
        Assert.Null(exception.KohaStatus);
        await Assert.ThrowsAsync<KohaUnavailableException>(() => a.WaitAsync(Wait, TestCancellation));
        Assert.True(Assert.Single(tokens.Requests).CancellationToken.IsCancellationRequested);
        Assert.Empty(api.Requests);
    }

    // R21, EX-011: one deadline from the start of the call bounds every step: the token step; the token step and the
    // call together (the token takes 6 of the 10 seconds, so the call has 4 left); reading the body; the new token
    // after a 401; and the retry. One tick before the deadline the call is still running; at the deadline it ends in
    // "unavailable" without a status.
    [Theory]
    [InlineData("token")]
    [InlineData("token and call")]
    [InlineData("body")]
    [InlineData("new token")]
    [InlineData("retry")]
    public async Task Deadline_covers_every_step(string step)
    {
        var slowToken = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokens = new StubHttpMessageHandler(async (request, cancellationToken) => (step, request.Index) switch
        {
            ("token", _) => await Stall(cancellationToken),
            ("token and call", _) => await slowToken.Task,
            ("new token", 1) => await Stall(cancellationToken),
            _ => Token($"token-{request.Index + 1}"),
        });
        var api = new StubHttpMessageHandler(async (request, cancellationToken) => (step, request.Index) switch
        {
            ("body", _) => StubHttpMessageHandler.StallingBodyReply(HttpStatusCode.OK),
            ("new token" or "retry", 0) => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            _ => await Stall(cancellationToken),
        });
        using var harness = new KohaClientsHarness(tokens, api);

        var call = GetAsync(harness.CreateApiClient(), TestCancellation);
        switch (step)
        {
            case "token":
                await tokens.WaitForRequestsAsync(1);
                break;
            case "token and call":
                await tokens.WaitForRequestsAsync(1);
                harness.Time.Advance(TimeSpan.FromSeconds(6));
                slowToken.SetResult(Token("token-1"));
                await api.WaitForRequestsAsync(1);
                break;
            case "new token":
                await tokens.WaitForRequestsAsync(2);
                break;
            case "retry":
                await api.WaitForRequestsAsync(2);
                break;
            default:
                await api.WaitForRequestsAsync(1);
                break;
        }

        var elapsed = step == "token and call" ? TimeSpan.FromSeconds(6) : TimeSpan.Zero;
        harness.Time.Advance(KohaClientsHarness.Deadline - elapsed - TimeSpan.FromTicks(1));
        Assert.False(call.IsCompleted);
        harness.Time.Advance(TimeSpan.FromTicks(1));

        var exception = await Assert.ThrowsAsync<KohaUnavailableException>(() => call.WaitAsync(Wait, TestCancellation));
        Assert.Null(exception.KohaStatus);
        Assert.Equal(DependencyFailureOutcome.Unavailable, KohaProtocolFailureMapper.Map(exception));
    }

    // R20, call step: a body that breaks off while it is read arrives as a network error (an HttpRequestException
    // around the IOException), which is "unavailable", not an unusable response.
    [Fact]
    public async Task Body_breaking_off_arrives_as_network_error()
    {
        var api = new StubHttpMessageHandler(() => StubHttpMessageHandler.BrokenBodyReply(HttpStatusCode.OK));
        using var harness = new KohaClientsHarness(NumberedTokens(), api);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => GetAsync(harness.CreateApiClient(), TestCancellation).WaitAsync(Wait, TestCancellation));

        Assert.IsType<IOException>(exception.InnerException);
        Assert.Equal(DependencyFailureOutcome.Unavailable, KohaProtocolFailureMapper.Map(exception));
    }

    // R22, ER-009, EX-012: the caller's cancellation, while it waits for a token, during the protected call, while the
    // body is read, and during the retry, propagates as a cancellation that carries the caller's token, and cancels
    // the outbound request it reached. While waiting alone for a token, the token request is cancelled too (R18).
    [Theory]
    [InlineData("token")]
    [InlineData("call")]
    [InlineData("body")]
    [InlineData("retry")]
    public async Task Caller_cancellation_propagates(string step)
    {
        var tokens = new StubHttpMessageHandler(async (request, cancellationToken) => step == "token"
            ? await Stall(cancellationToken)
            : Token($"token-{request.Index + 1}"));
        var api = new StubHttpMessageHandler(async (request, cancellationToken) => (step, request.Index) switch
        {
            ("body", _) => StubHttpMessageHandler.StallingBodyReply(HttpStatusCode.OK),
            ("retry", 0) => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            _ => await Stall(cancellationToken),
        });
        using var harness = new KohaClientsHarness(tokens, api);
        using var caller = new CancellationTokenSource();

        var call = GetAsync(harness.CreateApiClient(), caller.Token);
        var reached = step switch
        {
            "token" => tokens,
            _ => api,
        };
        await reached.WaitForRequestsAsync(step == "retry" ? 2 : 1);
        await caller.CancelAsync();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => call.WaitAsync(Wait, TestCancellation));
        Assert.Equal(caller.Token, exception.CancellationToken);
        Assert.True(reached.Requests[^1].CancellationToken.IsCancellationRequested);
    }

    // R18, R22: a caller that cancels while it shares a token request with another caller stops alone; the token
    // request goes on, and the other caller receives the token and completes its call.
    [Fact]
    public async Task Caller_cancelling_a_shared_token_wait_leaves_the_other_caller()
    {
        var release = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokens = new StubHttpMessageHandler((_, _) => release.Task);
        var api = new StubHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.OK));
        using var harness = new KohaClientsHarness(tokens, api);
        var client = harness.CreateApiClient();
        using var caller = new CancellationTokenSource();

        var cancelled = GetAsync(client, caller.Token);
        await tokens.WaitForRequestsAsync(1);
        var other = GetAsync(client, TestCancellation);
        await caller.CancelAsync();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cancelled.WaitAsync(Wait, TestCancellation));
        Assert.Equal(caller.Token, exception.CancellationToken);
        Assert.False(tokens.Requests[0].CancellationToken.IsCancellationRequested);

        release.SetResult(Token("shared"));
        using var response = await other.WaitAsync(Wait, TestCancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer shared", Authorization(Assert.Single(api.Requests)));
        Assert.Single(tokens.Requests);
    }

    // R22: when the caller's cancellation and the deadline's expiry coincide, the caller's cancellation wins.
    [Fact]
    public async Task Caller_cancellation_wins_when_it_coincides_with_the_deadline()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await release.Task;
            cancellationToken.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var harness = new KohaClientsHarness(NumberedTokens(), api);
        using var caller = new CancellationTokenSource();

        var call = GetAsync(harness.CreateApiClient(), caller.Token);
        await api.WaitForRequestsAsync(1);
        await caller.CancelAsync();
        harness.Time.Advance(KohaClientsHarness.Deadline);
        release.SetResult();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => call.WaitAsync(Wait, TestCancellation));
        Assert.Equal(caller.Token, exception.CancellationToken);
    }

    // As a generated client sends: the headers first, then the body is read by the caller.
    private static Task<HttpResponseMessage> GetAsync(HttpClient client, CancellationToken cancellationToken) =>
        client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, Address),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

    // Waits until the request is cancelled, as a Koha that does not answer.
    private static async Task<HttpResponseMessage> Stall(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("Not reached.");
    }

    private static string? Authorization(StubHttpMessageHandler.RecordedRequest request) =>
        request.Headers
            .Where(header => string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
            .Select(header => header.Value)
            .SingleOrDefault();

    // Replies "token-1", "token-2", ... in the order the token requests arrive.
    private static StubHttpMessageHandler NumberedTokens() =>
        new((request, _) => Task.FromResult(Token($"token-{request.Index + 1}")));

    private static HttpResponseMessage Token(string accessToken) =>
        StubHttpMessageHandler.Reply(
            HttpStatusCode.OK,
            $$"""{"access_token":"{{accessToken}}","token_type":"Bearer","expires_in":3600}""");
}
