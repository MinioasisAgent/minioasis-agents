using Microsoft.Extensions.Time.Testing;
using Minioasis.Koha.Shared.Exceptions;
using Minioasis.Koha.Shared.Handlers;
using Minioasis.Koha.Shared.Options;
using Minioasis.Koha.Tests.Shared.TestDoubles;
using System.Net;
using Xunit;

namespace Minioasis.Koha.Tests.Shared.Handlers;

/// <summary>
/// Spec 001, R21 and <c>EX-011</c>: the deadline handler bounds the send and the reading of the whole body with one
/// deadline, and turns its own expiry into <see cref="KohaUnavailableException"/>, never into a caller cancellation
/// (<c>ER-009</c>). The handler is tested directly here (<c>TS-002</c>); the pipeline tests show it on both clients.
/// </summary>
public sealed class KohaDeadlineHandlerTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);
    private static readonly DateTimeOffset Start = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);
    private static readonly Uri Address = new("https://koha.test/api/v1/patrons");

    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    // R21: a reply that arrives in time is passed on, with its body already read.
    [Fact]
    public async Task Reply_in_time_is_passed_on_with_its_body()
    {
        var time = new FakeTimeProvider(Start);
        using var invoker = Invoker(new StubHttpMessageHandler(
            () => StubHttpMessageHandler.Reply(HttpStatusCode.OK, """{"ok":true}""")), time);

        using var response = await SendAsync(invoker, TestCancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"ok":true}""", await response.Content.ReadAsStringAsync(TestCancellation));
    }

    // R21: expiry while waiting for the response headers is "unavailable", without a status or an inner exception,
    // and the request is cancelled. One tick before the deadline, the request still waits.
    [Fact]
    public async Task Expiry_while_waiting_for_headers_is_unavailable()
    {
        var time = new FakeTimeProvider(Start);
        var stub = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Not reached.");
        });
        using var invoker = Invoker(stub, time);

        var send = SendAsync(invoker, TestCancellation);
        await stub.WaitForRequestsAsync(1);
        time.Advance(Deadline - TimeSpan.FromTicks(1));
        Assert.False(send.IsCompleted);
        time.Advance(TimeSpan.FromTicks(1));

        var exception = await Assert.ThrowsAsync<KohaUnavailableException>(
            () => send.WaitAsync(Wait, TestCancellation));
        Assert.Null(exception.KohaStatus);
        Assert.Null(exception.InnerException);
        Assert.True(stub.Requests[0].CancellationToken.IsCancellationRequested);
    }

    // R21, EX-011: the body is read inside the deadline, so a body that stalls ends in "unavailable" when the
    // deadline expires, and the response is disposed.
    [Fact]
    public async Task Expiry_while_reading_a_slow_body_is_unavailable()
    {
        var time = new FakeTimeProvider(Start);
        var reply = StubHttpMessageHandler.StallingBodyReply(HttpStatusCode.OK);
        var stub = new StubHttpMessageHandler(() => reply);
        using var invoker = Invoker(stub, time);

        var send = SendAsync(invoker, TestCancellation);
        await stub.WaitForRequestsAsync(1);
        Assert.False(send.IsCompleted);
        time.Advance(Deadline);

        var exception = await Assert.ThrowsAsync<KohaUnavailableException>(
            () => send.WaitAsync(Wait, TestCancellation));
        Assert.Null(exception.KohaStatus);
        Assert.Null(exception.InnerException);
        Assert.Throws<ObjectDisposedException>(() => reply.Content.ReadAsStream(TestCancellation));
    }

    // R18, R21: the deadline counts from the moment the request is sent, not from the moment the handler was made.
    // The fake Koha refuses a request whose token is already cancelled, as a real connection would.
    [Fact]
    public async Task Deadline_counts_from_the_send()
    {
        var time = new FakeTimeProvider(Start);
        using var invoker = Invoker(new StubHttpMessageHandler((_, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(StubHttpMessageHandler.Reply(HttpStatusCode.OK, "{}"));
        }), time);
        time.Advance(Deadline * 2);

        using var response = await SendAsync(invoker, TestCancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // R22, ER-009: the caller's cancellation, while the headers or the body are awaited, propagates as a cancellation,
    // not as "unavailable", and cancels the request.
    [Theory]
    [InlineData("headers")]
    [InlineData("body")]
    public async Task Caller_cancellation_propagates(string step)
    {
        var time = new FakeTimeProvider(Start);
        var reply = StubHttpMessageHandler.StallingBodyReply(HttpStatusCode.OK);
        var stub = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            if (step == "headers")
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return reply;
        });
        using var invoker = Invoker(stub, time);
        using var caller = new CancellationTokenSource();

        var send = SendAsync(invoker, caller.Token);
        await stub.WaitForRequestsAsync(1);
        await caller.CancelAsync();

        var exception = await Record.ExceptionAsync(() => send.WaitAsync(Wait, TestCancellation));
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.True(stub.Requests[0].CancellationToken.IsCancellationRequested);
    }

    // R22: when the caller's cancellation and the deadline's expiry coincide, the caller's cancellation wins.
    [Fact]
    public async Task Caller_cancellation_wins_when_it_coincides_with_the_deadline()
    {
        var time = new FakeTimeProvider(Start);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stub = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await release.Task;
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Not reached.");
        });
        using var invoker = Invoker(stub, time);
        using var caller = new CancellationTokenSource();

        var send = SendAsync(invoker, caller.Token);
        await stub.WaitForRequestsAsync(1);
        await caller.CancelAsync();
        time.Advance(Deadline);
        release.SetResult();

        var exception = await Record.ExceptionAsync(() => send.WaitAsync(Wait, TestCancellation));
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }

    // R20, call step: a network error is passed on unchanged, so that it is reported as "unavailable"; a body that
    // breaks off arrives as an HttpRequestException around the IOException, and the response is disposed.
    [Theory]
    [InlineData("connect")]
    [InlineData("body")]
    public async Task Network_error_is_passed_on(string failure)
    {
        var time = new FakeTimeProvider(Start);
        var thrown = new HttpRequestException("The test connection failed.");
        var reply = StubHttpMessageHandler.BrokenBodyReply(HttpStatusCode.OK);
        var stub = new StubHttpMessageHandler((_, _) =>
            failure == "connect" ? Task.FromException<HttpResponseMessage>(thrown) : Task.FromResult(reply));
        using var invoker = Invoker(stub, time);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => SendAsync(invoker, TestCancellation).WaitAsync(Wait, TestCancellation));

        if (failure == "connect")
        {
            Assert.Same(thrown, exception);
        }
        else
        {
            Assert.IsType<IOException>(exception.InnerException);
            Assert.Throws<ObjectDisposedException>(() => reply.Content.ReadAsStream(TestCancellation));
        }
    }

    [Fact]
    public void Null_arguments_throw()
    {
        var time = new FakeTimeProvider(Start);

        Assert.Equal(
            "options",
            Assert.Throws<ArgumentNullException>(() => new KohaDeadlineHandler(null!, time)).ParamName);
        Assert.Equal(
            "timeProvider",
            Assert.Throws<ArgumentNullException>(() => new KohaDeadlineHandler(Options(), null!)).ParamName);
    }

    private static HttpMessageInvoker Invoker(HttpMessageHandler inner, TimeProvider time) =>
        new(new KohaDeadlineHandler(Options(), time) { InnerHandler = inner });

    private static Task<HttpResponseMessage> SendAsync(HttpMessageInvoker invoker, CancellationToken cancellationToken) =>
        invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, Address), cancellationToken);

    private static KohaOptions Options() => new()
    {
        BaseUrl = new Uri("https://koha.test/api/v1/"),
        ClientId = "dummy-client-id",
        ClientSecret = "dummy-client-secret",
        Deadline = Deadline,
    };
}
