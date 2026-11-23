using Minioasis.Koha.Shared.Handlers;
using Minioasis.Koha.Tests.Shared.TestDoubles;
using System.Net;
using Xunit;

namespace Minioasis.Koha.Tests.Shared.Handlers;

public sealed class KohaDeadlineHandlerTests
{
    private readonly FakeTimeProvider time = new();

    [Fact]
    public async Task Returns_the_response_with_its_body_already_read()
    {
        var body = new GatedStream("""{"item_id":1}""");
        using var invoker = Invoker((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(body),
        }));

        var sending = SendAsync(invoker, CancellationToken.None);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(sending.IsCompleted);

        body.Open();
        using var response = await sending.WaitAsync(Wait.Limit, TestContext.Current.CancellationToken);
        Assert.True(body.Completed);
    }

    [Fact]
    public async Task Reports_the_expired_deadline_while_waiting_for_the_response_as_a_timeout()
    {
        using var invoker = Invoker(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var sending = SendAsync(invoker, CancellationToken.None);
        await time.WaitForPendingTimersAsync(1);

        time.Advance(TimeSpan.FromSeconds(10) - TimeSpan.FromTicks(1));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(sending.IsCompleted);

        time.Advance(TimeSpan.FromTicks(1));
        var exception = await Assert.ThrowsAsync<TimeoutException>(() => sending.WaitAsync(Wait.Limit, TestContext.Current.CancellationToken));
        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
    }

    [Fact]
    public async Task Reports_the_expired_deadline_while_reading_the_body_as_a_timeout()
    {
        var body = new GatedStream("{}");
        using var invoker = Invoker((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(body),
        }));

        var sending = SendAsync(invoker, CancellationToken.None);
        await time.WaitForPendingTimersAsync(1);
        time.Advance(TimeSpan.FromSeconds(10));

        await Assert.ThrowsAsync<TimeoutException>(() => sending.WaitAsync(Wait.Limit, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Lets_the_callers_cancellation_travel_up_as_a_cancellation()
    {
        using var caller = new CancellationTokenSource();
        using var invoker = Invoker(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var sending = SendAsync(invoker, caller.Token);
        await time.WaitForPendingTimersAsync(1);
        await caller.CancelAsync();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sending.WaitAsync(Wait.Limit, TestContext.Current.CancellationToken));
        Assert.Equal(caller.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task Reports_any_failure_after_the_caller_cancelled_as_a_cancellation()
    {
        using var caller = new CancellationTokenSource();
        using var invoker = Invoker(async (_, _) =>
        {
            await caller.CancelAsync();
            throw new InvalidOperationException("A failure that the caller's cancellation caused.");
        });

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => SendAsync(invoker, caller.Token).WaitAsync(Wait.Limit, TestContext.Current.CancellationToken));
        Assert.Equal(caller.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task Passes_any_other_failure_through_unchanged()
    {
        var failure = new HttpRequestException("Connection refused.");
        using var invoker = Invoker((_, _) => Task.FromException<HttpResponseMessage>(failure));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => SendAsync(invoker, CancellationToken.None).WaitAsync(Wait.Limit, TestContext.Current.CancellationToken));
        Assert.Same(failure, exception);
    }

    [Fact]
    public async Task Releases_its_timer_when_the_request_completes()
    {
        using var invoker = Invoker((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        using var response = await SendAsync(invoker, CancellationToken.None)
            .WaitAsync(Wait.Limit, TestContext.Current.CancellationToken);

        Assert.Equal(0, time.PendingTimers);
    }

    private HttpMessageInvoker Invoker(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) =>
        new(new KohaDeadlineHandler(time) { InnerHandler = new StubHttpMessageHandler(respond) });

    private static async Task<HttpResponseMessage> SendAsync(HttpMessageInvoker invoker, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://koha.test/api/v1/items/1");
        return await invoker.SendAsync(request, cancellationToken);
    }

    /// <summary>A body that delivers nothing until the test opens it, and honours cancellation while it waits.</summary>
    private sealed class GatedStream(string text) : Stream
    {
        private readonly TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly MemoryStream inner = new(System.Text.Encoding.UTF8.GetBytes(text));

        public bool Completed
        {
            get; private set;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public void Open() => gate.TrySetResult();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await gate.Task.WaitAsync(cancellationToken);
            var read = await inner.ReadAsync(buffer, cancellationToken);
            Completed = read == 0;
            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
