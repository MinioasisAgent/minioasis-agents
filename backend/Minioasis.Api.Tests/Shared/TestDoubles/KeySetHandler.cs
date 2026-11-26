using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Minioasis.Api.Tests.Shared.TestDoubles;

/// <summary>
/// Stands in for Cloudflare's key address (TS-004): it records each fetch and answers as the test chooses. By default
/// it publishes the default signing key.
/// </summary>
internal sealed class KeySetHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<Uri> requests = new();
    private volatile Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond;

    public KeySetHandler()
    {
        respond = (_, _) => Task.FromResult(Json(TestSigningKey.KeySet(TestSigningKey.Default)));
    }

    /// <summary>The address of each fetch, in order.</summary>
    public IReadOnlyList<Uri> Requests => [.. requests];

    /// <summary>From now on, publishes <paramref name="keys"/>.</summary>
    public void Publish(params TestSigningKey[] keys) => AnswerWith(_ => Json(TestSigningKey.KeySet(keys)));

    /// <summary>From now on, answers every fetch with the response that <paramref name="answer"/> builds.</summary>
    public void AnswerWith(Func<HttpRequestMessage, HttpResponseMessage> answer) =>
        respond = (request, _) => Task.FromResult(answer(request));

    /// <summary>From now on, answers every fetch with what <paramref name="answer"/> does.</summary>
    public void AnswerWith(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer) => respond = answer;

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        requests.Enqueue(request.RequestUri!);
        var response = await respond(request, cancellationToken);
        response.RequestMessage ??= request;
        return response;
    }
}
