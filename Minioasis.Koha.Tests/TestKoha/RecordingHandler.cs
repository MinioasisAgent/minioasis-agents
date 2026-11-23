using System.Collections.Concurrent;
using System.Net;

namespace Minioasis.Koha.Tests.TestKoha;

/// <summary>
/// Records each request that reaches the test Koha and the status it answered.
/// </summary>
internal sealed class RecordingHandler : DelegatingHandler
{
    private readonly ConcurrentQueue<Exchange> exchanges = new();

    public IReadOnlyList<Exchange> Exchanges => [.. exchanges];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);
        exchanges.Enqueue(new Exchange(request.Method, request.RequestUri!, request.Headers.Authorization?.Scheme, body, response.StatusCode));
        return response;
    }

    public sealed record Exchange(
        HttpMethod Method,
        Uri Uri,
        string? AuthorizationScheme,
        string? Body,
        HttpStatusCode Status)
    {
        /// <summary>Leaves out the body, so that a failed assertion that prints an exchange prints no credential.</summary>
        public override string ToString() => $"{Method} {Uri.AbsolutePath} -> {(int)Status}";
    }
}
