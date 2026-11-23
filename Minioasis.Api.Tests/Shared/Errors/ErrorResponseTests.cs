using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Minioasis.Api.Tests.Items.TestDoubles;
using Minioasis.Api.Tests.Shared.TestDoubles;
using Minioasis.Application.Items.Abstractions;
using Minioasis.Application.Items.Outcomes;
using Minioasis.Application.Items.Results;
using System.Net;
using Xunit;

namespace Minioasis.Api.Tests.Shared.Errors;

public sealed class ErrorResponseTests
{
    private const string ExceptionText = "Koha host db.internal refused user minioasis";

    public static TheoryData<string> Environments => new() { "Development", "Production" };

    [Theory]
    [MemberData(nameof(Environments))]
    public async Task A_returned_server_failure_is_masked_in_every_environment(string environment)
    {
        await using var factory = HostWith(
            new FakeGetItemGateway(GetItemGatewayResult.Failed(GetItemOutcome.Unavailable)),
            environment);

        using var response = await factory.CreateClient().GetAsync("/api/v1/items/12", TestContext.Current.CancellationToken);

        await ProblemAssert.IsMaskedAsync(response, HttpStatusCode.ServiceUnavailable);
    }

    [Theory]
    [MemberData(nameof(Environments))]
    public async Task An_unhandled_exception_is_masked_as_500_in_every_environment(string environment)
    {
        await using var factory = HostWith(
            new FakeGetItemGateway((_, _) => throw new InvalidOperationException(ExceptionText)),
            environment);

        using var response = await factory.CreateClient().GetAsync("/api/v1/items/12", TestContext.Current.CancellationToken);

        await ProblemAssert.IsMaskedAsync(response, HttpStatusCode.InternalServerError);
        Assert.DoesNotContain(
            "db.internal",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(GetItemOutcome.AuthenticationFailed, "koha.authentication_failed")]
    [InlineData(GetItemOutcome.InvalidResponse, "koha.invalid_response")]
    [InlineData(GetItemOutcome.Unavailable, "koha.unavailable")]
    public async Task The_log_holds_the_internal_code_and_the_operation_with_the_trace_id_of_the_response(
        GetItemOutcome outcome,
        string code)
    {
        await using var factory = HostWith(new FakeGetItemGateway(GetItemGatewayResult.Failed(outcome)));

        using var response = await factory.CreateClient().GetAsync("/api/v1/items/12", TestContext.Current.CancellationToken);

        var traceId = (await ProblemAssert.IsMaskedAsync(response, response.StatusCode)).GetProperty("traceId").GetString();
        var entry = Assert.Single(factory.Logs.Entries, entry => entry.Values.ContainsKey("ErrorCode"));
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(code, entry.Values["ErrorCode"]);
        Assert.Equal("items.get", entry.Values["Operation"]);
        Assert.Equal(traceId, entry.Values["TraceId"]);
    }

    [Fact]
    public async Task The_log_holds_the_unhandled_exception_with_the_trace_id_of_the_response()
    {
        var failure = new InvalidOperationException(ExceptionText);
        await using var factory = HostWith(new FakeGetItemGateway((_, _) => throw failure));

        using var response = await factory.CreateClient().GetAsync("/api/v1/items/12", TestContext.Current.CancellationToken);

        var traceId = (await ProblemAssert.IsMaskedAsync(response, HttpStatusCode.InternalServerError))
            .GetProperty("traceId").GetString();
        var entry = Assert.Single(factory.Logs.Entries, entry => entry.Level >= LogLevel.Error);
        Assert.Same(failure, entry.Exception);
        Assert.Equal("system.unexpected", entry.Values["ErrorCode"]);
        Assert.Equal(traceId, entry.Values["TraceId"]);
    }

    [Fact]
    public async Task A_client_error_is_not_logged_as_a_failure()
    {
        await using var factory = HostWith(new FakeGetItemGateway(GetItemGatewayResult.Failed(GetItemOutcome.NotFound)));

        using var response = await factory.CreateClient().GetAsync("/api/v1/items/12", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(factory.Logs.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Theory]
    [InlineData("/nope")]
    [InlineData("/api/v1/items/")]
    [InlineData("/api/v1/items")]
    [InlineData("/api/v1/items/12/extra")]
    public async Task An_unknown_path_returns_404_not_found(string path)
    {
        await using var factory = new ApiTestHostFactory();

        using var response = await factory.CreateClient().GetAsync(path, TestContext.Current.CancellationToken);

        await ProblemAssert.HasShapeAsync(
            response,
            HttpStatusCode.NotFound,
            "not_found",
            "Not found",
            "The requested resource does not exist.");
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task A_method_the_route_does_not_support_returns_405_with_the_allow_header(string method)
    {
        await using var factory = new ApiTestHostFactory();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/v1/items/1");

        using var response = await factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        await ProblemAssert.HasShapeAsync(
            response,
            HttpStatusCode.MethodNotAllowed,
            "method_not_allowed",
            "Method not allowed",
            "This resource does not support the request method.");
        Assert.Equal(["GET"], response.Content.Headers.Allow);
    }

    [Fact]
    public async Task When_the_caller_cancels_no_failure_is_logged_and_no_error_response_is_written()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new FakeGetItemGateway(async (_, token) =>
        {
            entered.SetResult();

            // Bounded, so that a request that ignores the cancellation ends and fails the test rather than hang it (TS-007).
            await Task.Delay(TimeSpan.FromSeconds(3), token);
            return GetItemGatewayResult.Failed(GetItemOutcome.Unavailable);
        });
        var finished = new TaskCompletionSource<ResponseRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var factory = HostWith(gateway, configureServices: services =>
            services.AddSingleton<IStartupFilter>(new ResponseRecordingStartupFilter(finished)));
        using var caller = new CancellationTokenSource();

        var sending = factory.CreateClient().GetAsync("/api/v1/items/12", caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await caller.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sending);
        var record = await finished.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Null(record.ContentType);
        Assert.Equal(0, record.BodyLength);
        Assert.NotEqual(StatusCodes.Status500InternalServerError, record.Status);
        Assert.NotEqual(StatusCodes.Status503ServiceUnavailable, record.Status);
        Assert.DoesNotContain(factory.Logs.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    private static ApiTestHostFactory HostWith(
        IGetItemGateway gateway,
        string environment = "Production",
        Action<IServiceCollection>? configureServices = null) =>
        new(
            services =>
            {
                services.RemoveAll<IGetItemGateway>();
                services.AddSingleton(gateway);
                configureServices?.Invoke(services);
            },
            environment: environment);

    private sealed record ResponseRecord(int Status, string? ContentType, long BodyLength);

    /// <summary>Records what the server wrote, as the outermost middleware sees it.</summary>
    private sealed class ResponseRecordingStartupFilter(TaskCompletionSource<ResponseRecord> finished) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                var original = context.Response.Body;
                using var copy = new MemoryStream();
                context.Response.Body = copy;
                try
                {
                    await nextMiddleware(context);
                }
                finally
                {
                    context.Response.Body = original;
                    finished.TrySetResult(new ResponseRecord(context.Response.StatusCode, context.Response.ContentType, copy.Length));
                }
            });
            next(app);
        };
    }
}
