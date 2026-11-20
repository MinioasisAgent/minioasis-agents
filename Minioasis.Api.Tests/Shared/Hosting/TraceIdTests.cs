using Microsoft.Extensions.Logging;
using Minioasis.Api.Tests.Shared.Factories;
using Minioasis.Api.Tests.Shared.TestDoubles;
using System.Diagnostics;
using System.Net;
using Xunit;

namespace Minioasis.Api.Tests.Shared.Hosting;

[Collection(nameof(ListenerFreeCollection))]
public sealed class TraceIdTests
{
    [Theory]
    [InlineData("client-visible error", "/does-not-exist", LogLevel.Information)]
    [InlineData("Application server failure", "/test/failures/koha.unavailable", LogLevel.Error)]
    [InlineData("unhandled exception", "/test/throw", LogLevel.Error)]
    public async Task Response_trace_id_is_w3c_and_matches_the_log(string failure, string path, LogLevel level)
    {
        await using var factory = new ApiTestHostFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        var traceId = await ReadTraceIdAsync(response);
        await factory.Logs.WaitForRequestFinishedAsync();

        Assert.Matches(ProblemResponseAssert.TraceIdPattern, traceId);
        var entry = Assert.Single(factory.Logs.MinioasisEntries, entry => entry.Level == level);
        Assert.True(entry.Properties.ContainsKey("TraceId"), failure);
        Assert.Equal(traceId, entry.Properties["TraceId"]);
    }

    [Fact]
    public async Task Two_requests_get_different_ids()
    {
        await using var factory = new ApiTestHostFactory();
        using var client = factory.CreateClient();

        using var first = await client.GetAsync("/does-not-exist", TestContext.Current.CancellationToken);
        using var second = await client.GetAsync("/does-not-exist", TestContext.Current.CancellationToken);

        Assert.NotEqual(await ReadTraceIdAsync(first), await ReadTraceIdAsync(second));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Trace_id_present_when_logging_is_disabled(string environment)
    {
        using (var probe = new ActivitySource("Microsoft.AspNetCore"))
        {
            Assert.False(probe.HasListeners(), "Another listener for Microsoft.AspNetCore is alive.");
        }

        await using var factory = new ApiTestHostFactory(environment: environment, logging: false);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/does-not-exist", TestContext.Current.CancellationToken);

        var traceId = await ProblemResponseAssert.ClientVisibleAsync(
            response,
            HttpStatusCode.NotFound,
            "not_found",
            "Not found",
            "No resource exists at this address.");
        Assert.Matches(ProblemResponseAssert.TraceIdPattern, traceId);
    }

    private static async Task<string> ReadTraceIdAsync(HttpResponseMessage response)
    {
        Assert.Equal(ProblemResponseAssert.ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var body = System.Text.Json.JsonDocument.Parse(text);
        return body.RootElement.GetProperty("traceId").GetString()!;
    }
}
