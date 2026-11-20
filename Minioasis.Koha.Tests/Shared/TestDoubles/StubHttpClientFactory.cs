using System.Collections.Concurrent;

namespace Minioasis.Koha.Tests.Shared.TestDoubles;

/// <summary>
/// An <see cref="IHttpClientFactory"/> double: every client it creates sends through <paramref name="handler"/>, and
/// it records the name each client was asked for, so a test can see which named client a component used.
/// </summary>
public sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    private readonly ConcurrentQueue<string> names = new();

    public IReadOnlyList<string> CreatedNames => [.. names];

    public HttpClient CreateClient(string name)
    {
        names.Enqueue(name);
        return new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
    }
}
