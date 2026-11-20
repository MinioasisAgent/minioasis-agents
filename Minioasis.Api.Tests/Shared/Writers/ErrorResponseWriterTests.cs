using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Minioasis.Api.Shared.Factories;
using Minioasis.Api.Shared.Models;
using Minioasis.Api.Shared.Providers;
using Minioasis.Api.Shared.Writers;
using Minioasis.Api.Tests.Shared.TestDoubles;
using Minioasis.Application.Shared.Errors;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Minioasis.Api.Tests.Shared.Writers;

public sealed class ErrorResponseWriterTests
{
    /// <summary>
    /// R5, ER-009: a client that disconnected before the response started must not turn the one <c>Error</c> entry
    /// of an unhandled exception into several, so the writer does not write with the request's abort token.
    /// </summary>
    [Fact]
    public async Task Unhandled_exception_after_the_client_disconnected_logs_one_error_and_does_not_throw()
    {
        var logs = new RecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(logs);
        });
        var writer = new ErrorResponseWriter(
            CreateMessageProvider(),
            loggerFactory.CreateLogger<ErrorResponseWriter>());
        using var activity = new Activity("test").SetIdFormat(ActivityIdFormat.W3C).Start();
        var context = new DefaultHttpContext { RequestAborted = new CancellationToken(canceled: true) };
        context.Features.Set<IHttpActivityFeature>(new TestActivityFeature(activity));
        using var body = new MemoryStream();
        context.Response.Body = body;

        await writer.WriteUnhandledAsync(context, new InvalidOperationException("after the disconnect"))
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var error = Assert.Single(logs.Entries, entry => entry.Level >= LogLevel.Error);
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.Equal(500, context.Response.StatusCode);
        using var written = JsonDocument.Parse(body.ToArray());
        Assert.Equal("internal_error", written.RootElement.GetProperty("code").GetString());
        Assert.Equal(activity.Id, written.RootElement.GetProperty("traceId").GetString());
    }

    private static ErrorMessageProvider CreateMessageProvider() =>
        new(ErrorMessageCatalogFactory.Create(
            new ErrorMessageSource(Encoding.UTF8.GetBytes("{}")),
            new ApplicationErrorDefinitionRegistry(
                new ApplicationErrorDefinitionSource([SystemErrorDefinitions.Unexpected]))));

    private sealed class TestActivityFeature(Activity activity) : IHttpActivityFeature
    {
        public Activity Activity { get; set; } = activity;
    }
}
