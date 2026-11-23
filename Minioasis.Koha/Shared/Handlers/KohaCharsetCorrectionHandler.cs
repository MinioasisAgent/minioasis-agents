namespace Minioasis.Koha.Shared.Handlers;

/// <summary>
/// Corrects the charset <c>utf8</c>, which Koha sends and .NET cannot read, to <c>utf-8</c>.
/// </summary>
/// <remarks>
/// Test Koha, 26.05, 2026-11-23: a 401 carries <c>Content-Type: application/json; charset=utf8</c>. Without the
/// correction, reading the body throws, and the status of the answer would be lost.
/// </remarks>
internal sealed class KohaCharsetCorrectionHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        var contentType = response.Content.Headers.ContentType;
        if (string.Equals(contentType?.CharSet?.Trim('"'), "utf8", StringComparison.OrdinalIgnoreCase))
        {
            contentType!.CharSet = "utf-8";
        }

        return response;
    }
}
