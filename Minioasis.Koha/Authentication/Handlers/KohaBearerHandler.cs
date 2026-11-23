using Minioasis.Koha.Authentication.Exceptions;
using Minioasis.Koha.Authentication.Providers;
using System.Net;
using System.Net.Http.Headers;

namespace Minioasis.Koha.Authentication.Handlers;

/// <summary>
/// Attaches the access token, and after a 401 on a GET gets a new token and sends the request once more (EX-009).
/// No other method is ever sent again (EX-010).
/// </summary>
internal sealed class KohaBearerHandler(KohaAccessTokenProvider tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized || request.Method != HttpMethod.Get)
        {
            return response;
        }

        response.Dispose();
        tokens.Invalidate(token);
        var retry = CreateRetry(request, await GetTokenAsync(cancellationToken));
        return await base.SendAsync(retry, cancellationToken);
    }

    /// <summary>
    /// Waits for a token. A wait that is cancelled, when the deadline expires or the caller leaves, means no token
    /// was obtained in time, which is an authentication failure (EX-013). The deadline handler above reports it as a
    /// cancellation when the caller left.
    /// </summary>
    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await tokens.GetAsync(cancellationToken);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new KohaAuthenticationException("No Koha token was obtained before the request was cancelled.", exception);
        }
    }

    private static HttpRequestMessage CreateRetry(HttpRequestMessage original, string token)
    {
        var retry = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version,
            VersionPolicy = original.VersionPolicy,
        };

        foreach (var header in original.Headers.Where(header => header.Key != "Authorization"))
        {
            retry.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in original.Options)
        {
            retry.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return retry;
    }
}
