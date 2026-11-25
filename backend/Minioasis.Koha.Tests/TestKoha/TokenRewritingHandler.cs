using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Minioasis.Koha.Tests.TestKoha;

/// <summary>
/// Replaces the access token in Koha's token answers with one that Koha will refuse, so that a check can make
/// Koha answer 401: the first answer only, or every answer.
/// </summary>
internal sealed class TokenRewritingHandler(bool everyAnswer) : DelegatingHandler
{
    private int rewritten;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK || (!everyAnswer && rewritten > 0))
        {
            return response;
        }

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))!;
        json["access_token"] = $"minioasis-invalid-{Guid.NewGuid():N}";
        rewritten++;
        var content = new StringContent(json.ToJsonString(), Encoding.UTF8, "application/json");
        response.Content.Dispose();
        response.Content = content;
        return response;
    }
}
