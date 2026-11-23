using System.Collections.Concurrent;

namespace Minioasis.Koha.Tests.Shared.TestDoubles;

/// <summary>
/// A transport that records each request and answers with the response that the test chooses.
/// </summary>
internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    private readonly ConcurrentQueue<RecordedRequest> requests = new();

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : this((request, _) => Task.FromResult(respond(request)))
    {
    }

    public IReadOnlyList<RecordedRequest> Requests => [.. requests];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        requests.Enqueue(new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.Authorization,
            body,
            request.Content?.Headers.ContentType?.MediaType,
            request.Headers));

        var response = await respond(request, cancellationToken);
        response.RequestMessage ??= request;
        return response;
    }
}
