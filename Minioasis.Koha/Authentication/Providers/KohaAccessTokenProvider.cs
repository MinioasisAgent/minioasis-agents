using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minioasis.Koha.Authentication.Exceptions;
using Minioasis.Koha.Generated.OAuth;
using Minioasis.Koha.Generated.OAuth.Contracts;
using Minioasis.Koha.Shared.Http;
using Minioasis.Koha.Shared.Options;
using System.Text.Json;

namespace Minioasis.Koha.Authentication.Providers;

/// <summary>
/// Obtains, validates, caches, and shares the Koha access token (EX-006, EX-008, EX-013).
/// </summary>
/// <remarks>
/// The client identifier and secret travel in the HTTP Basic header that the OAuth client carries; the form body
/// holds only the grant type. A token is reused while more than 60 seconds of its lifetime remain. Callers that
/// need a token at the same moment wait for one shared request, which is bounded by its own deadline, so that a
/// caller that leaves does not cancel it for the others. A token request is never retried.
/// </remarks>
internal sealed partial class KohaAccessTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<KohaOptions> options,
    TimeProvider timeProvider,
    ILogger<KohaAccessTokenProvider> logger)
{
    private const int MaximumLoggedErrorLength = 64;

    private static readonly TimeSpan RenewalMargin = TimeSpan.FromSeconds(60);

    private readonly Lock gate = new();
    private CachedToken? cached;
    private Task<CachedToken>? refresh;

    /// <summary>
    /// Returns a valid access token.
    /// </summary>
    /// <exception cref="KohaAuthenticationException">
    /// A token could not be obtained, for any reason other than <paramref name="cancellationToken"/> being cancelled.
    /// </exception>
    public async Task<string> GetAsync(CancellationToken cancellationToken)
    {
        Task<CachedToken> pending;
        lock (gate)
        {
            if (cached is not null && IsUsable(cached))
            {
                return cached.Value;
            }

            if (refresh is null || refresh.IsCompleted)
            {
                refresh = Task.Run(RequestTokenAsync, CancellationToken.None);
            }

            pending = refresh;
        }

        var token = await pending.WaitAsync(cancellationToken);
        return token.Value;
    }

    /// <summary>
    /// Discards the cached token if it is <paramref name="rejectedToken"/>, so that the next caller gets a new one.
    /// A newer token that another caller has already obtained is kept.
    /// </summary>
    public void Invalidate(string rejectedToken)
    {
        lock (gate)
        {
            if (cached is not null && cached.Value == rejectedToken)
            {
                cached = null;
            }
        }
    }

    private bool IsUsable(CachedToken token) => token.ExpiresAt - timeProvider.GetUtcNow() > RenewalMargin;

    private async Task<CachedToken> RequestTokenAsync()
    {
        var requestedAt = timeProvider.GetUtcNow();
        using var deadline = new CancellationTokenSource(KohaOptions.Deadline, timeProvider);

        Response response;
        try
        {
            var client = new GeneratedKohaOAuthClient(httpClientFactory.CreateClient(KohaClientNames.OAuth))
            {
                BaseUrl = options.Value.ApiBaseAddress.AbsoluteUri,
            };
            response = await client.TokenOAuthAsync(new Body { Grant_type = "client_credentials" }, deadline.Token);
        }
        catch (KohaServiceException exception)
        {
            LogTokenRefused(exception.StatusCode, LoggableError(exception), options.Value.ClientId);
            throw new KohaAuthenticationException("Koha refused the token request.", exception);
        }
        catch (Exception exception)
        {
            LogTokenRequestFailed(exception.GetType().Name, (exception as HttpRequestException)?.HttpRequestError, options.Value.ClientId);
            throw new KohaAuthenticationException("The Koha token request failed.", exception);
        }

        if (!IsValid(response))
        {
            LogTokenRejected(options.Value.ClientId);
            throw new KohaAuthenticationException("Koha answered the token request with an unusable token.");
        }

        var token = new CachedToken(response.Access_token, requestedAt + TimeSpan.FromSeconds(response.Expires_in));
        lock (gate)
        {
            cached = token;
        }

        return token;
    }

    private static bool IsValid(Response response) =>
        !string.IsNullOrWhiteSpace(response.Access_token)
        && response.Expires_in > 0
        && string.Equals(response.Token_type, "Bearer", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The <c>error</c> word of Koha's answer, when it is at most 64 characters of the set that RFC 6749,
    /// section 5.2, allows for an error code. Anything else is not logged.
    /// </summary>
    private static string? LoggableError(KohaServiceException exception)
    {
        var error = exception is KohaServiceException<Error> typed ? typed.Result.Error1 : ReadError(exception.Response);
        return error is { Length: > 0 and <= MaximumLoggedErrorLength } && error.All(IsErrorCodeCharacter)
            ? error
            : null;
    }

    private static string? ReadError(string? body)
    {
        try
        {
            using var document = JsonDocument.Parse(body ?? string.Empty);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.String
                    ? error.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsErrorCodeCharacter(char character) =>
        character is >= '\x20' and <= '\x7E' and not '"' and not '\\';

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Koha refused the token request with status {StatusCode} and error {OAuthError} for client {ClientId}.")]
    private partial void LogTokenRefused(int statusCode, string? oAuthError, string? clientId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The Koha token request failed with {FailureType} ({HttpRequestError}) for client {ClientId}.")]
    private partial void LogTokenRequestFailed(string failureType, HttpRequestError? httpRequestError, string? clientId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Koha answered the token request for client {ClientId} with a token that is blank, expired, or not Bearer.")]
    private partial void LogTokenRejected(string? clientId);

    private sealed record CachedToken(string Value, DateTimeOffset ExpiresAt);
}
