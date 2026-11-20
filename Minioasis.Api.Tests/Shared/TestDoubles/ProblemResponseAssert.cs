using System.Net;
using System.Text.Json;
using Xunit;

namespace Minioasis.Api.Tests.Shared.TestDoubles;

/// <summary>
/// Assertions on the two error body shapes: client-visible (R2) and masked (R3).
/// </summary>
public static class ProblemResponseAssert
{
    public const string TraceIdPattern = "^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$";

    public const string ProblemJson = "application/problem+json";

    /// <summary>Asserts R2 exactly, and returns the body's <c>traceId</c>.</summary>
    public static async Task<string> ClientVisibleAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code,
        string title,
        string detail)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await ReadProblemAsync(response);
        Assert.Equal(
            ["code", "detail", "status", "title", "traceId"],
            body.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal));
        AssertStatusMember(body, status);
        Assert.Equal(title, body.GetProperty("title").GetString());
        Assert.Equal(detail, body.GetProperty("detail").GetString());
        Assert.Equal(code, body.GetProperty("code").GetString());
        return AssertTraceIdMember(body);
    }

    /// <summary>Asserts R3 exactly, and returns the body's <c>traceId</c>.</summary>
    public static async Task<string> MaskedAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await ReadProblemAsync(response);
        Assert.Equal(
            ["code", "status", "title", "traceId"],
            body.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal));
        AssertStatusMember(body, status);
        Assert.Equal("Internal error", body.GetProperty("title").GetString());
        Assert.Equal("internal_error", body.GetProperty("code").GetString());
        return AssertTraceIdMember(body);
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);
        var body = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal(JsonValueKind.Object, body.ValueKind);
        return body;
    }

    private static void AssertStatusMember(JsonElement body, HttpStatusCode status)
    {
        var member = body.GetProperty("status");
        Assert.Equal(JsonValueKind.Number, member.ValueKind);
        Assert.Equal((int)status, member.GetInt32());
    }

    private static string AssertTraceIdMember(JsonElement body)
    {
        var member = body.GetProperty("traceId");
        Assert.Equal(JsonValueKind.String, member.ValueKind);
        var traceId = member.GetString()!;
        Assert.Matches(TraceIdPattern, traceId);
        return traceId;
    }
}
