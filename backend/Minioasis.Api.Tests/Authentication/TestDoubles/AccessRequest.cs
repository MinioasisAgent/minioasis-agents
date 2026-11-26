using Minioasis.Api.Tests.Shared;
using System.Net.Http.Headers;
using Xunit;

namespace Minioasis.Api.Tests.Authentication.TestDoubles;

/// <summary>
/// Sends one request with exactly the access headers that a test names, so that each test controls the token and
/// <c>X-Requested-With</c> itself.
/// </summary>
internal static class AccessRequest
{
    public const string ItemPath = "/api/v1/items/12";

    public static async Task<HttpResponseMessage> SendAsync(
        ApiTestHostFactory factory,
        string? token,
        string? requestedWith = ApiTestHostFactory.RequestedWithValue,
        HttpMethod? method = null,
        string path = ItemPath,
        Action<HttpRequestHeaders>? headers = null,
        CancellationToken? cancellationToken = null)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
        if (token is not null)
        {
            request.Headers.Add(ApiTestHostFactory.TokenHeader, token);
        }

        if (requestedWith is not null)
        {
            request.Headers.Add(ApiTestHostFactory.RequestedWithHeader, requestedWith);
        }

        headers?.Invoke(request.Headers);
        using var client = factory.CreateClient();
        return await client.SendAsync(request, cancellationToken ?? TestContext.Current.CancellationToken);
    }
}
