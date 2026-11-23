namespace Minioasis.Koha.Tests.TestKoha;

/// <summary>
/// Adds the two Cloudflare Access headers that the test Koha requires on every request (TS-013). The application's
/// code does not know about them.
/// </summary>
internal sealed class CloudflareAccessHandler(TestKohaSettings settings) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Set(request, "CF-Access-Client-Id", settings.CloudflareClientId);
        Set(request, "CF-Access-Client-Secret", settings.CloudflareClientSecret);
        return base.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Replaces the header rather than adding to it: a request sent again after a 401 is a copy of the first,
    /// which already carries the header.
    /// </summary>
    private static void Set(HttpRequestMessage request, string name, string value)
    {
        request.Headers.Remove(name);
        request.Headers.Add(name, value);
    }
}
