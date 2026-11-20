using System.Net;
using System.Text;

namespace Minioasis.Koha.Tests.Shared.TestDoubles;

/// <summary>
/// An <see cref="HttpMessageHandler"/> double (<c>TS-004</c>): it records every request, with its headers, its body,
/// and the cancellation token it was sent with, and answers with the reply the test scripts. It knows no Koha path
/// or payload; the tests supply them.
/// </summary>
public sealed class StubHttpMessageHandler(
    Func<StubHttpMessageHandler.RecordedRequest, CancellationToken, Task<HttpResponseMessage>> reply)
    : HttpMessageHandler
{
    private readonly Lock gate = new();
    private readonly List<RecordedRequest> requests = [];
    private readonly List<(int Count, TaskCompletionSource Arrived)> waiters = [];

    /// <summary>Answers every request at once with a new reply from <paramref name="reply"/>.</summary>
    public StubHttpMessageHandler(Func<HttpResponseMessage> reply)
        : this((_, _) => Task.FromResult(reply()))
    {
    }

    public IReadOnlyList<RecordedRequest> Requests
    {
        get
        {
            lock (gate)
            {
                return [.. requests];
            }
        }
    }

    /// <summary>A reply with a body, sent as <c>application/json</c> whatever it contains.</summary>
    public static HttpResponseMessage Reply(HttpStatusCode status, string body) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    /// <summary>A reply whose body breaks off with an <see cref="IOException"/> when it is read.</summary>
    public static HttpResponseMessage BrokenBodyReply(HttpStatusCode status) =>
        new(status)
        {
            Content = new BrokenContent()
        };

    /// <summary>
    /// A reply that has already arrived in full: its body is read to the end even when the request has been cancelled
    /// in the meantime, as when the last bytes were received just before the cancellation.
    /// </summary>
    public static HttpResponseMessage ArrivedReply(HttpStatusCode status, string body) =>
        new(status)
        {
            Content = new ArrivedContent(Encoding.UTF8.GetBytes(body))
        };

    /// <summary>
    /// Completes once <paramref name="count"/> requests have arrived, and fails after five seconds (<c>TS-007</c>).
    /// </summary>
    public Task WaitForRequestsAsync(int count)
    {
        lock (gate)
        {
            if (requests.Count >= count)
            {
                return Task.CompletedTask;
            }

            var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            waiters.Add((count, arrived));
            return arrived.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var headers = request.Headers
            .Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
            .Select(header => new KeyValuePair<string, string>(header.Key, string.Join(", ", header.Value)))
            .ToList();

        RecordedRequest recorded;
        lock (gate)
        {
            recorded = new RecordedRequest(
                requests.Count,
                request.Method,
                request.RequestUri,
                headers,
                request.Content?.Headers.ContentType?.MediaType,
                body,
                cancellationToken);
            requests.Add(recorded);
            foreach (var waiter in waiters.Where(waiter => requests.Count >= waiter.Count).ToList())
            {
                waiters.Remove(waiter);
                waiter.Arrived.TrySetResult();
            }
        }

        var response = await reply(recorded, cancellationToken);
        response.RequestMessage = request;
        return response;
    }

    /// <param name="Index">The position of the request in arrival order, from 0.</param>
    /// <param name="CancellationToken">The token the request was sent with, as the handler received it.</param>
    public sealed record RecordedRequest(
        int Index,
        HttpMethod Method,
        Uri? Uri,
        IReadOnlyList<KeyValuePair<string, string>> Headers,
        string? ContentType,
        string? Body,
        CancellationToken CancellationToken);

    // Like a connection lost mid-body: the read stream delivers the first bytes, then throws, as SocketsHttpHandler's
    // stream does (an IOException, not wrapped in an HttpRequestException).
    private sealed class BrokenContent : HttpContent
    {
        protected override Task<Stream> CreateContentReadStreamAsync() =>
            Task.FromResult<Stream>(new BrokenStream());

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            new BrokenStream().CopyToAsync(stream);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class ArrivedContent(byte[] body) : HttpContent
    {
        protected override Task<Stream> CreateContentReadStreamAsync() =>
            Task.FromResult<Stream>(new ArrivedStream(body));

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(body).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = body.Length;
            return true;
        }
    }

    // Reads ignore the cancellation token: the bytes are already here.
    private sealed class ArrivedStream(byte[] body) : MemoryStream(body, writable: false)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Read(buffer.Span));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Task.FromResult(Read(buffer, offset, count));
    }

    private sealed class BrokenStream : Stream
    {
        private readonly MemoryStream start = new(Encoding.UTF8.GetBytes("""{"access_token":"""));

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = start.Read(buffer, offset, count);
            return read > 0 ? read : throw new IOException("The test body broke off.");
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = start.Read(buffer.Span);
            return read > 0
                ? ValueTask.FromResult(read)
                : ValueTask.FromException<int>(new IOException("The test body broke off."));
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
