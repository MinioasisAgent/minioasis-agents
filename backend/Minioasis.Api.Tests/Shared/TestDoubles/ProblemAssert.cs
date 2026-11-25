using System.Net;
using System.Text.Json;
using Xunit;

namespace Minioasis.Api.Tests.Shared.TestDoubles;

/// <summary>
/// Checks the one error shape of brief 001, part 3 (ER-005), and returns the body.
/// </summary>
internal static class ProblemAssert
{
    public static async Task<JsonElement> HasShapeAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code,
        string title,
        string? detail)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .RootElement.Clone();
        string[] expected = detail is null
            ? ["code", "status", "title", "traceId"]
            : ["code", "detail", "status", "title", "traceId"];
        Assert.Equal(expected, body.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));

        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.Equal(title, body.GetProperty("title").GetString());
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
        if (detail is not null)
        {
            Assert.Equal(detail, body.GetProperty("detail").GetString());
        }

        return body;
    }

    public static Task<JsonElement> IsMaskedAsync(HttpResponseMessage response, HttpStatusCode status) =>
        HasShapeAsync(response, status, "internal_error", "Internal error", detail: null);
}
