using Minioasis.Koha.Authentication.Exceptions;
using Minioasis.Koha.Generated.OAuth;
using Minioasis.Koha.Generated.OAuth.Contracts;
using Minioasis.Koha.Shared.Clients;
using Minioasis.Koha.Shared.Exceptions;
using Minioasis.Koha.Shared.Options;

namespace Minioasis.Koha.Authentication.Providers;

/// <summary>
/// Gets the access token for protected Koha calls (spec 001, R15 to R18; <c>EX-006</c>, <c>EX-008</c>). It requests
/// the token with the client-credentials grant, through the generated OAuth client on the
/// <see cref="KohaClientNames.OAuth"/> client; checks the reply; caches the token until 30 seconds before it expires;
/// and lets every caller that needs a new token at the same moment wait for one shared request.
/// </summary>
/// <remarks>
/// The shared request runs on its own cancellation, not on any caller's: a caller that cancels stops only its own
/// wait, and the request is cancelled when the last waiting caller has stopped waiting. Its result is then dropped,
/// and the next call sends a new request (R18). A failed request is never cached.
/// </remarks>
public sealed class KohaAccessTokenProvider
{
    // Plan, "Agent's discretion": a token is refreshed 30 seconds before it expires (R17).
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(30);

    private const string ClientCredentialsGrant = "client_credentials";
    private const string BearerTokenType = "Bearer";
    private const int TokenReplyStatus = 200;

    private readonly IHttpClientFactory httpClientFactory;
    private readonly KohaOptions options;
    private readonly TimeProvider timeProvider;
    private readonly Lock gate = new();

    // Both are read and written only while holding the gate.
    private CachedToken? cached;
    private SharedTokenRequest? pending;

    public KohaAccessTokenProvider(IHttpClientFactory httpClientFactory, KohaOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.httpClientFactory = httpClientFactory;
        this.options = options;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Returns the cached token while it is valid; otherwise waits for the shared token request, starting it when
    /// none is in flight.
    /// </summary>
    /// <exception cref="KohaAuthenticationException">
    /// Koha answered with a status other than 200, 429, or 500 to 599, or with a 200 reply that is not a valid token.
    /// </exception>
    /// <exception cref="KohaUnavailableException">
    /// Koha answered 429 or 500 to 599, or could not be reached (<see cref="HttpRequestException"/> or
    /// <see cref="IOException"/>).
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled. Only this caller stops waiting.
    /// </exception>
    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        SharedTokenRequest request;
        bool starts;
        lock (gate)
        {
            if (cached is not null && timeProvider.GetElapsedTime(cached.SentAt) < cached.UsableFor)
            {
                return cached.AccessToken;
            }

            starts = pending is null;
            request = pending ??= new SharedTokenRequest();
            request.Waiters++;
        }

        // Started outside the gate, so that a reply that completes at once cannot finish the request while this
        // caller still holds the gate.
        if (starts)
        {
            _ = RunAsync(request);
        }

        try
        {
            return await request.Token.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // This caller's cancellation, which propagates unchanged (ER-009). The shared request itself never ends
            // in a cancellation while a caller waits for it, and Leave changes nothing for a request that has ended.
            Leave(request);
            throw;
        }
    }

    /// <summary>
    /// Discards the cached token if it is still <paramref name="token"/>, so that the next call requests a new one. A
    /// newer token that another call has already obtained is kept (R19, <c>EX-009</c>).
    /// </summary>
    public void Invalidate(string token)
    {
        lock (gate)
        {
            if (string.Equals(cached?.AccessToken, token, StringComparison.Ordinal))
            {
                cached = null;
            }
        }
    }

    // Never throws: the outcome, success or failure, is handed to every caller that waits for it.
    private async Task RunAsync(SharedTokenRequest request)
    {
        IssuedToken token;
        try
        {
            token = await RequestTokenAsync(request.Cancellation.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            lock (gate)
            {
                if (ReferenceEquals(pending, request))
                {
                    pending = null;
                }
            }

            request.Token.SetException(exception);
            return;
        }

        lock (gate)
        {
            // A request that every caller abandoned is no longer pending: its result is not cached (R18).
            if (ReferenceEquals(pending, request))
            {
                pending = null;
                cached = new CachedToken(token.AccessToken, token.SentAt, token.Lifetime - RefreshMargin);
            }
        }

        request.Token.SetResult(token.AccessToken);
    }

    // A caller stopped waiting. When it was the last one, the request is abandoned: it is cancelled, and the next
    // call starts a new one. The request may already have finished at the same moment, and a newer one may be
    // pending; neither is touched then.
    private void Leave(SharedTokenRequest request)
    {
        lock (gate)
        {
            request.Waiters--;
            if (request.Waiters > 0 || !ReferenceEquals(pending, request))
            {
                return;
            }

            pending = null;
        }

        request.Cancellation.Cancel();
    }

    private async Task<IssuedToken> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var sentAt = timeProvider.GetTimestamp();
        using var httpClient = httpClientFactory.CreateClient(KohaClientNames.OAuth);

        // Plan, "Approach": the base comes from the options; the generated default ("/api/v1") is never relied on.
        var client = new GeneratedKohaOAuthClient(httpClient) { BaseUrl = options.BaseUrl.AbsoluteUri };
        var body = new Body
        {
            Grant_type = ClientCredentialsGrant,
            Client_id = options.ClientId,
            Client_secret = options.ClientSecret,
        };

        Response reply;
        try
        {
            reply = await client.TokenOAuthAsync(body, cancellationToken).ConfigureAwait(false);
        }
        catch (KohaServiceException exception)
        {
            // R23: the generated exception's message and text hold Koha's body, which may hold a token. Only the
            // status is passed on; the exception itself is neither wrapped nor logged.
            throw RefusedTokenRequest(exception.StatusCode);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            throw new KohaUnavailableException(exception);
        }

        return Validate(reply, sentAt);
    }

    // R20, token step: 429 and 500 to 599 mean Koha could not serve the request; any other status, 200 included (a
    // reply that cannot be parsed), means Koha gave no usable token.
    private static Exception RefusedTokenRequest(int status) => status is 429 or (>= 500 and <= 599)
        ? new KohaUnavailableException(status)
        : new KohaAuthenticationException(status);

    // R16, EX-008. expires_in is an int in the generated contract, so a string, a fraction, or a number above
    // 2147483647 never gets here: the generated client fails to parse it, which is a refusal with status 200 above.
    private static IssuedToken Validate(Response reply, long sentAt)
    {
        if (string.IsNullOrWhiteSpace(reply.Access_token)
            || reply.Expires_in < 1
            || !string.Equals(reply.Token_type, BearerTokenType, StringComparison.OrdinalIgnoreCase))
        {
            throw new KohaAuthenticationException(TokenReplyStatus);
        }

        return new IssuedToken(reply.Access_token, sentAt, TimeSpan.FromSeconds(reply.Expires_in));
    }

    // SentAt is a timestamp of the time provider, which a change of the system clock does not move. The lifetime is
    // counted from the moment the request was sent, which is never later than the moment Koha issued the token.
    private sealed record IssuedToken(string AccessToken, long SentAt, TimeSpan Lifetime);

    // Reused while less than UsableFor (the lifetime minus the refresh margin) has passed since SentAt (R17). A token
    // whose lifetime is not longer than the margin has no usable time left: it serves only the callers of the request
    // that obtained it, and the next call requests a new one, as if it had not been cached.
    private sealed record CachedToken(string AccessToken, long SentAt, TimeSpan UsableFor);

    private sealed class SharedTokenRequest
    {
        public TaskCompletionSource<string> Token
        {
            get;
        } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Not disposed: it has no timer and no linked token, so it holds nothing to release, and the last waiting
        // caller may cancel it from another thread at any moment.
        public CancellationTokenSource Cancellation
        {
            get;
        } = new();

        // Read and written only while holding the provider's gate.
        public int Waiters
        {
            get;
            set;
        }
    }
}
