using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Minioasis.Application.Shared.Errors;
using System.Net;

namespace Minioasis.Api.Authentication.Providers;

/// <summary>
/// Fetches and reads the signing keys that Cloudflare Access publishes at <c>&lt;team domain&gt;/cdn-cgi/access/certs</c>
/// (brief 002, part 3). That address is a key set, not an OpenID Connect discovery document, so these few lines take
/// the place of the library's own retriever. The library's configuration manager calls them: it holds the keys,
/// makes the requests that need a fetch wait for one shared fetch, and decides when to fetch again.
/// </summary>
/// <remarks>
/// A fetch is one request through the named client <see cref="ClientName"/>, bounded by <see cref="Deadline"/>, with
/// no retry (EX-011). It fails when the address cannot be reached in time, answers another status than 200, or returns
/// a body that is not a key set with at least one RS256 key; every failure is logged with
/// <c>access.keys_unavailable</c> and its cause, and then thrown to the configuration manager.
/// </remarks>
internal sealed partial class CloudflareAccessKeySetProvider(
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider,
    ILogger<CloudflareAccessKeySetProvider> logger)
    : IConfigurationRetriever<OpenIdConnectConfiguration>, IDocumentRetriever
{
    public const string ClientName = "CloudflareAccess";

    /// <summary>The one deadline on a fetch, from sending the request to reading the whole body.</summary>
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    /// <summary>Fetches the key set and reads its RS256 signing keys.</summary>
    /// <remarks>
    /// The configuration manager passes <see cref="CancellationToken.None"/>: the fetch belongs to every request that
    /// waits for it, so no caller's cancellation stops it. Only <see cref="Deadline"/> does.
    /// </remarks>
    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
        string address,
        IDocumentRetriever retriever,
        CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(retriever);

        try
        {
            return ReadKeySet(await retriever.GetDocumentAsync(address, cancel));
        }
        catch (Exception exception)
        {
            LogFetchFailed(
                AccessErrorDefinitions.KeysUnavailable.Code,
                exception.GetType().Name,
                (exception as HttpRequestException)?.HttpRequestError,
                (int?)(exception as HttpRequestException)?.StatusCode);
            throw;
        }
    }

    /// <summary>Makes the one request for the key set and returns its body.</summary>
    /// <exception cref="HttpRequestException">The address answered another status than 200, or could not be reached.</exception>
    /// <exception cref="OperationCanceledException">The deadline passed.</exception>
    public async Task<string> GetDocumentAsync(string address, CancellationToken cancel)
    {
        using var deadline = new CancellationTokenSource(Deadline, timeProvider);
        var client = httpClientFactory.CreateClient(ClientName);

        // The default completion option reads the whole body before it returns, so the deadline covers the body too.
        using var response = await client.GetAsync(address, deadline.Token);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new HttpRequestException(
                HttpRequestError.Unknown,
                "The key set address did not answer 200.",
                inner: null,
                response.StatusCode);
        }

        return await response.Content.ReadAsStringAsync(deadline.Token);
    }

    /// <summary>
    /// The RS256 keys of the key set: those with the key type RSA and the algorithm RS256 that the library can use.
    /// </summary>
    /// <exception cref="ArgumentException">The body is not a key set.</exception>
    /// <exception cref="InvalidDataException">The key set holds no RS256 key.</exception>
    private static OpenIdConnectConfiguration ReadKeySet(string document)
    {
        var published = new JsonWebKeySet(document);
        var rs256 = new JsonWebKeySet();
        foreach (var key in published.Keys.Where(key =>
            key.Kty == JsonWebAlgorithmsKeyTypes.RSA && key.Alg == SecurityAlgorithms.RsaSha256))
        {
            rs256.Keys.Add(key);
        }

        var configuration = new OpenIdConnectConfiguration { JsonWebKeySet = rs256 };
        foreach (var signingKey in rs256.GetSigningKeys())
        {
            configuration.SigningKeys.Add(signingKey);
        }

        return configuration.SigningKeys.Count > 0
            ? configuration
            : throw new InvalidDataException("The key set holds no RS256 key.");
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "{ErrorCode}: the Cloudflare Access signing keys could not be fetched: {FailureType} ({HttpRequestError}), status {StatusCode}.")]
    private partial void LogFetchFailed(string errorCode, string failureType, HttpRequestError? httpRequestError, int? statusCode);
}
