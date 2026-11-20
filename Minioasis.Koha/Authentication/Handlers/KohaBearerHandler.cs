using Minioasis.Koha.Authentication.Providers;
using System.Net;
using System.Net.Http.Headers;

namespace Minioasis.Koha.Authentication.Handlers;

/// <summary>
/// Attaches <c>Authorization: Bearer &lt;token&gt;</c> to every protected Koha call (spec 001, R19; <c>EX-004</c>).
/// After a 401, it discards the token that got it, unless another call has already replaced it. A <c>GET</c> is then
/// sent once more, as a new request with a new token, and the reply to that second attempt is final
/// (<c>EX-009</c>). A request with any other method is never sent again (<c>EX-010</c>).
/// </summary>
/// <remarks>
/// It runs inside the deadline handler on the API client, so the token step and the retry share the call's deadline
/// (R21). Its failures are the token provider's documented exceptions and the caller's cancellation.
/// </remarks>
public sealed class KohaBearerHandler : DelegatingHandler
{
    private const string BearerScheme = "Bearer";

    private readonly KohaAccessTokenProvider tokens;

    public KohaBearerHandler(KohaAccessTokenProvider tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        this.tokens = tokens;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await tokens.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, token);
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // Kept if another call already obtained a newer token (R19).
        tokens.Invalidate(token);
        if (request.Method != HttpMethod.Get)
        {
            return response;
        }

        response.Dispose();
        var newToken = await tokens.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        return await base.SendAsync(CreateRetry(request, newToken), cancellationToken).ConfigureAwait(false);
    }

    // EX-009: the request is built again, with the same method, address, and headers, and the new token. That is
    // all a generated client sets on a GET, which carries no body.
    private static HttpRequestMessage CreateRetry(HttpRequestMessage request, string token)
    {
        var retry = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            retry.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        retry.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, token);
        return retry;
    }
}
