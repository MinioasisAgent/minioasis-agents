using Microsoft.Extensions.Logging;
using Minioasis.Api.Tests.Shared.Factories;
using Minioasis.Api.Tests.Shared.TestDoubles;
using System.Net;
using Xunit;

namespace Minioasis.Api.Tests.Shared.Writers;

public sealed class ErrorLoggingTests
{
    public static readonly TheoryData<string, int?> ApplicationServerFailures = new()
    {
        { "koha.authentication_failed", 401 },
        { "koha.authentication_failed", null },
        { "koha.invalid_response", 200 },
        { "koha.invalid_response", null },
        { "koha.unavailable", 503 },
        { "koha.unavailable", null },
        { "system.unexpected", null },
    };

    public static readonly TheoryData<string, string> ClientVisibleRequests = new()
    {
        { TestRequests.UnknownPath, "not_found" },
        { TestRequests.WrongMethodOnOneMethodRoute, "method_not_allowed" },
        { TestRequests.MalformedJson, "request.invalid" },
        { "capability", "test.not_found" },
    };

    public static readonly TheoryData<string, string> FrameworkErrorRequests = Combine(
        [
            TestRequests.UnknownPath,
            TestRequests.RootPath,
            TestRequests.WrongMethodOnOneMethodRoute,
            TestRequests.WrongMethodOnTwoMethodRoute,
            TestRequests.MalformedJson,
            TestRequests.WrongTypeForField,
            TestRequests.EmptyBody,
            TestRequests.MissingContentType,
            TestRequests.TextPlain,
            TestRequests.OverSizeLimit,
            TestRequests.NonNumericRouteValue,
            TestRequests.MissingQueryValue,
            TestRequests.RouteConstraintMismatch,
        ],
        ["Development", "Production"]);

    [Theory]
    [MemberData(nameof(ApplicationServerFailures))]
    public async Task Server_failure_logs_one_error_with_code_and_trace_id(string code, int? dependencyStatus)
    {
        await using var factory = new ApiTestHostFactory();
        using var client = factory.CreateClient();
        var query = dependencyStatus is null ? string.Empty : $"?dependencyStatus={dependencyStatus}";

        using var response = await client.GetAsync(
            $"/test/failures/{code}{query}",
            TestContext.Current.CancellationToken);
        var traceId = await ProblemResponseAssert.MaskedAsync(response, response.StatusCode);
        await factory.Logs.WaitForRequestFinishedAsync();

        var error = Assert.Single(factory.Logs.Entries, entry => entry.Level >= LogLevel.Error);
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.StartsWith("Minioasis", error.Category, StringComparison.Ordinal);
        Assert.Contains(code, error.Message, StringComparison.Ordinal);
        Assert.Contains(traceId, error.Message, StringComparison.Ordinal);
        Assert.Equal(dependencyStatus, error.Properties["DependencyStatus"]);
    }

    [Theory]
    [InlineData("/test/throw", "Development")]
    [InlineData("/test/throw", "Production")]
    [InlineData("/test/ambiguous", "Development")]
    [InlineData("/test/failures/test.not_found", "Production")]
    public async Task Exception_logs_one_error_across_all_categories(string path, string environment)
    {
        await using var factory = new ApiTestHostFactory(TestErrorDefinitions.Replace, environment: environment);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        var traceId = await ProblemResponseAssert.MaskedAsync(response, HttpStatusCode.InternalServerError);
        await factory.Logs.WaitForRequestFinishedAsync();

        var error = Assert.Single(factory.Logs.Entries, entry => entry.Level >= LogLevel.Error);
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.StartsWith("Minioasis", error.Category, StringComparison.Ordinal);
        Assert.NotNull(error.Exception);
        Assert.Contains(traceId, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exception_entry_carries_the_thrown_exception()
    {
        await using var factory = new ApiTestHostFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/test/throw", TestContext.Current.CancellationToken);
        await factory.Logs.WaitForRequestFinishedAsync();

        var error = Assert.Single(factory.Logs.Entries, entry => entry.Level >= LogLevel.Error);
        Assert.Equal(TestEndpointModule.ExceptionMarker, error.Exception?.Message);
    }

    [Theory]
    [MemberData(nameof(ClientVisibleRequests))]
    public async Task Client_visible_error_logs_one_information_entry(string request, string code)
    {
        await using var factory = new ApiTestHostFactory(TestErrorDefinitions.Replace);
        using var client = factory.CreateClient();
        using var message = request == "capability"
            ? new HttpRequestMessage(HttpMethod.Get, "/test/failures/test.not_found?id=7")
            : TestRequests.Create(request);

        using var response = await client.SendAsync(message, TestContext.Current.CancellationToken);
        var traceId = await ReadTraceIdAsync(response);
        await factory.Logs.WaitForRequestFinishedAsync();

        var entry = Assert.Single(factory.Logs.MinioasisEntries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains(code, entry.Message, StringComparison.Ordinal);
        Assert.Contains(traceId, entry.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(FrameworkErrorRequests))]
    public async Task Client_visible_error_logs_nothing_at_warning_or_above_in_any_category(
        string request,
        string environment)
    {
        await using var factory = new ApiTestHostFactory(environment: environment);
        using var client = factory.CreateClient();
        using var message = TestRequests.Create(request);

        using var response = await client.SendAsync(message, TestContext.Current.CancellationToken);
        await factory.Logs.WaitForRequestFinishedAsync();

        Assert.True((int)response.StatusCode is 400 or 404 or 405, $"Status {(int)response.StatusCode}");
        Assert.DoesNotContain(factory.Logs.Entries, entry => entry.Level >= LogLevel.Warning);
        Assert.Contains(factory.Logs.MinioasisEntries, entry => entry.Level == LogLevel.Information);
    }

    private static async Task<string> ReadTraceIdAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var body = System.Text.Json.JsonDocument.Parse(text);
        return body.RootElement.GetProperty("traceId").GetString()!;
    }

    private static TheoryData<string, string> Combine(string[] requests, string[] environments)
    {
        var data = new TheoryData<string, string>();
        foreach (var request in requests)
        {
            foreach (var environment in environments)
            {
                data.Add(request, environment);
            }
        }

        return data;
    }
}
