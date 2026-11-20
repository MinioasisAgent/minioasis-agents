using System.Text;
using Xunit;

namespace Minioasis.Api.Tests.Shared.TestDoubles;

/// <summary>
/// Named requests to the routes of <see cref="TestEndpointModule"/>, shared by the framework-error and logging
/// tests, so that a theory row reads as the case the spec lists.
/// </summary>
public static class TestRequests
{
    public const string UnknownPath = "unknown path";
    public const string UnknownPathUnderKnownPrefix = "unknown path under a known prefix";
    public const string RootPath = "root path";
    public const string WrongMethodOnOneMethodRoute = "DELETE on a one-method route";
    public const string WrongMethodOnTwoMethodRoute = "DELETE on a two-method route";
    public const string MalformedJson = "malformed JSON";
    public const string WrongTypeForField = "a JSON value of the wrong type for a field";
    public const string EmptyBody = "an empty body";
    public const string MissingContentType = "a missing Content-Type";
    public const string TextPlain = "text/plain";
    public const string OverSizeLimit = "a body over the size limit";
    public const string NonNumericRouteValue = "a non-numeric value for a numeric route parameter";
    public const string MissingQueryValue = "a missing required query value";
    public const string NonNumericQueryValue = "a non-numeric value for a numeric query parameter";
    public const string RouteConstraintMismatch = "a value that fails a route constraint";

    public const string JsonBody = """{"number":5}""";

    public static HttpRequestMessage Create(string name) => name switch
    {
        UnknownPath => new HttpRequestMessage(HttpMethod.Get, "/does-not-exist"),
        UnknownPathUnderKnownPrefix => new HttpRequestMessage(HttpMethod.Get, "/test/does-not-exist"),
        RootPath => new HttpRequestMessage(HttpMethod.Get, "/"),
        WrongMethodOnOneMethodRoute => new HttpRequestMessage(HttpMethod.Delete, "/test/one"),
        WrongMethodOnTwoMethodRoute => new HttpRequestMessage(HttpMethod.Delete, "/test/two"),
        MalformedJson => Body("""{"number":""", "application/json"),
        WrongTypeForField => Body("""{"number":"five"}""", "application/json"),
        EmptyBody => Body(string.Empty, "application/json"),
        MissingContentType => Body(JsonBody, contentType: null),
        TextPlain => Body(JsonBody, "text/plain; charset=utf-8"),
        OverSizeLimit => OverSize(),
        NonNumericRouteValue => new HttpRequestMessage(HttpMethod.Get, "/test/convert/abc"),
        MissingQueryValue => new HttpRequestMessage(HttpMethod.Get, "/test/query"),
        NonNumericQueryValue => new HttpRequestMessage(HttpMethod.Get, "/test/query?page=abc"),
        RouteConstraintMismatch => new HttpRequestMessage(HttpMethod.Get, "/test/items/abc"),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown test request."),
    };

    /// <summary>A <c>POST /test/body</c> with the body and the <c>Content-Type</c> exactly as given.</summary>
    public static HttpRequestMessage Body(string body, string? contentType)
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
        if (contentType is not null)
        {
            Assert.True(content.Headers.TryAddWithoutValidation("Content-Type", contentType));
        }

        return new HttpRequestMessage(HttpMethod.Post, "/test/body") { Content = content };
    }

    private static HttpRequestMessage OverSize()
    {
        var request = Body(JsonBody, "application/json");
        request.Headers.Add(OversizedBodyStartupFilter.HeaderName, "true");
        return request;
    }
}
