using System.Net.Http.Headers;

namespace Minioasis.Koha.Tests.Shared.TestDoubles;

/// <summary>
/// What a stub handler received, captured when the request was sent.
/// </summary>
internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri Uri,
    AuthenticationHeaderValue? Authorization,
    string? Body,
    string? MediaType,
    HttpRequestHeaders Headers);
