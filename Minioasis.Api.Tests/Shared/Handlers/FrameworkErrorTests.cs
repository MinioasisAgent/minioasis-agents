using Microsoft.Extensions.Logging;
using Minioasis.Api.Tests.Shared.Factories;
using Minioasis.Api.Tests.Shared.TestDoubles;
using System.Net;
using Xunit;

namespace Minioasis.Api.Tests.Shared.Handlers;

public sealed class FrameworkErrorTests
{
    private const string NotFoundTitle = "Not found";
    private const string NotFoundDetail = "No resource exists at this address.";
    private const string MethodNotAllowedTitle = "Method not allowed";
    private const string MethodNotAllowedDetail = "This address does not support this request method.";
    private const string InvalidTitle = "Invalid request";
    private const string InvalidDetail = "The request could not be read.";

    public static readonly TheoryData<string, string, string[]> WrongMethods = new()
    {
        { "DELETE", "/test/one", ["GET"] },
        { "POST", "/test/one", ["GET"] },
        { "OPTIONS", "/test/one", ["GET"] },
        { "DELETE", "/test/two", ["GET", "POST"] },
        { "PUT", "/test/two", ["GET", "POST"] },
    };

    public static readonly TheoryData<string, string> UnreadableBodies = InEachEnvironment(
        TestRequests.MalformedJson,
        TestRequests.WrongTypeForField,
        TestRequests.EmptyBody,
        TestRequests.MissingContentType,
        TestRequests.TextPlain,
        TestRequests.OverSizeLimit);

    public static readonly TheoryData<string, string> UnconvertibleOrMissingParameters = InEachEnvironment(
        TestRequests.NonNumericRouteValue,
        TestRequests.MissingQueryValue,
        TestRequests.NonNumericQueryValue);

    [Theory]
    [InlineData(TestRequests.UnknownPath)]
    [InlineData(TestRequests.UnknownPathUnderKnownPrefix)]
    [InlineData(TestRequests.RootPath)]
    public async Task Unknown_path_is_not_found(string request)
    {
        await using var factory = new ApiTestHostFactory();
        using var client = factory.CreateClient();
        using var message = TestRequests.Create(request);

        using var response = await client.SendAsync(message, TestContext.Current.CancellationToken);

        await ProblemResponseAssert.ClientVisibleAsync(
            response, HttpStatusCode.NotFound, "not_found", NotFoundTitle, NotFoundDetail);
    }

    [Fact]
    public async Task Head_on_unknown_path_is_not_found()
    {
        await using var factory = new ApiTestHostFactory();
        using var client = factory.CreateClient();
        using var message = new HttpRequestMessage(HttpMethod.Head, "/does-not-exist");

        using var response = await client.SendAsync(message, TestContext.Current.CancellationToken);
        await factory.Logs.WaitForRequestFinishedAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ProblemResponseAssert.ProblemJson, response.Content.Headers.ContentType?.MediaType);
        AssertOneInformationEntry(factory, "not_found");
    }

    [Theory]
    [MemberData(nameof(WrongMethods))]
    public async Task Wrong_method_is_405_with_allow_header(string method, string path, string[] allowed)
    {
        await using var factory = new ApiTestHostFactory();
        using var client = factory.CreateClient();
        using var message = new HttpRequestMessage(new HttpMethod(method), path);

        using var response = await client.SendAsync(message, TestContext.Current.CancellationToken);

        await ProblemResponseAssert.ClientVisibleAsync(
            response,
            HttpStatusCode.MethodNotAllowed,
            "method_not_allowed",
            MethodNotAllowedTitle,
            MethodNotAllowedDetail);
        Assert.Equal(allowed, response.Content.Headers.Allow.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("/test/one", new[] { "GET" })]
    [InlineData("/test/two", new[] { "GET", "POST" })]
    public async Task Head_on_wrong_method_is_405_with_allow_header(string path, string[] allowed)
    {
        await using var factory = new ApiTestHostFactory();
        using var client = factory.CreateClient();
        using var message = new HttpRequestMessage(HttpMethod.Head, path);

        using var response = await client.SendAsync(message, TestContext.Current.CancellationToken);
        await factory.Logs.WaitForRequestFinishedAsync();

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(allowed, response.Content.Headers.Allow.Order(StringComparer.Ordinal));
        Assert.Equal(ProblemResponseAssert.ProblemJson, response.Content.Headers.ContentType?.MediaType);
        AssertOneInformationEntry(factory, "method_not_allowed");
    }

    [Theory]
    [MemberData(nameof(UnreadableBodies))]
    public async Task Unreadable_body_is_request_invalid(string request, string environment)
    {
        await using var factory = new ApiTestHostFactory(environment: environment);
        using var client = factory.CreateClient();
        using var message = TestRequests.Create(request);

        using var response = await client.SendAsync(message, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        await ProblemResponseAssert.ClientVisibleAsync(
            response, HttpStatusCode.BadRequest, "request.invalid", InvalidTitle, InvalidDetail);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("application/json; charset=utf-8")]
    [InlineData("Application/JSON")]
    [InlineData("application/problem+json")]
    [InlineData("application/vnd.minioasis.test+json; charset=utf-8")]
    public async Task Accepted_json_media_types_reach_the_endpoint(string contentType)
    {
        await using var factory = new ApiTestHostFactory();
        using var client = factory.CreateClient();
        using var message = TestRequests.Body(TestRequests.JsonBody, contentType);

        using var response = await client.SendAsync(message, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("5", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [MemberData(nameof(UnconvertibleOrMissingParameters))]
    public async Task Unconvertible_or_missing_parameter_is_request_invalid(string request, string environment)
    {
        await using var factory = new ApiTestHostFactory(environment: environment);
        using var client = factory.CreateClient();
        using var message = TestRequests.Create(request);

        using var response = await client.SendAsync(message, TestContext.Current.CancellationToken);

        await ProblemResponseAssert.ClientVisibleAsync(
            response, HttpStatusCode.BadRequest, "request.invalid", InvalidTitle, InvalidDetail);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Route_constraint_mismatch_is_not_found(string environment)
    {
        await using var factory = new ApiTestHostFactory(environment: environment);
        using var client = factory.CreateClient();
        using var message = TestRequests.Create(TestRequests.RouteConstraintMismatch);

        using var response = await client.SendAsync(message, TestContext.Current.CancellationToken);

        await ProblemResponseAssert.ClientVisibleAsync(
            response, HttpStatusCode.NotFound, "not_found", NotFoundTitle, NotFoundDetail);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(409)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task Other_empty_statuses_are_left_unchanged(int status)
    {
        await using var factory = new ApiTestHostFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/test/status/{status}", TestContext.Current.CancellationToken);

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Null(response.Content.Headers.ContentType);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>R5a for a <c>HEAD</c> request, whose body these tests do not read.</summary>
    private static void AssertOneInformationEntry(ApiTestHostFactory factory, string code)
    {
        var entry = Assert.Single(factory.Logs.MinioasisEntries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains(code, entry.Message, StringComparison.Ordinal);
    }

    private static TheoryData<string, string> InEachEnvironment(params string[] requests)
    {
        var data = new TheoryData<string, string>();
        foreach (var request in requests)
        {
            data.Add(request, "Development");
            data.Add(request, "Production");
        }

        return data;
    }
}
