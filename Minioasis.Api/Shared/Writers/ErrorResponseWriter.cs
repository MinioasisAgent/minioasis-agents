using Minioasis.Api.Shared.Factories;
using Minioasis.Api.Shared.Mappers;
using Minioasis.Api.Shared.Providers;
using Minioasis.Application.Shared.Errors;
using System.Text.Json.Nodes;

namespace Minioasis.Api.Shared.Writers;

/// <summary>
/// The one component that writes an error response and logs it (CD-006): one <c>Information</c> entry for a
/// client-visible error (R5a), one <c>Error</c> entry for a server failure (R5), each with the response's
/// <c>traceId</c> (R4).
/// </summary>
public sealed partial class ErrorResponseWriter(ErrorMessageProvider messages, ILogger<ErrorResponseWriter> logger)
{
    /// <summary>Writes an Application error with the status that the mapper chose for its category.</summary>
    /// <exception cref="InvalidOperationException">
    /// A client-visible error's placeholder values do not match its message. Nothing has been written or logged;
    /// the exception handler answers it (R2).
    /// </exception>
    public Task WriteAsync(HttpContext context, ApplicationError error, int status)
    {
        var code = error.Definition.Code;
        var traceId = ProblemResponseFactory.GetTraceId(context);
        if (error.Definition.IsClientVisible)
        {
            var body = ProblemResponseFactory.CreateClientVisible(status, code, messages.GetMessage(error), traceId);
            LogClientError(logger, code, status, traceId);
            return WriteBodyAsync(context, status, body);
        }

        LogServerFailure(logger, code, error.DependencyStatus, status, traceId);
        return WriteBodyAsync(context, status, ProblemResponseFactory.CreateMasked(status, traceId));
    }

    /// <summary>Writes the masked response to an unhandled exception, and logs the exception (R3, R5).</summary>
    public Task WriteUnhandledAsync(HttpContext context, Exception exception)
    {
        var definition = SystemErrorDefinitions.Unexpected;
        var status = ApplicationResultHttpMapper.GetStatus(definition.Category);
        var traceId = ProblemResponseFactory.GetTraceId(context);
        LogUnhandledException(logger, exception, definition.Code, status, traceId);
        return WriteBodyAsync(context, status, ProblemResponseFactory.CreateMasked(status, traceId));
    }

    private static Task WriteBodyAsync(HttpContext context, int status, JsonObject body)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = ProblemResponseFactory.ContentType;

        // The one place that decides whether an error response has a body. The approved spec (R2, R7) writes it
        // for every method, HEAD included; a change for HEAD is proposed separately and would be made here.
        return context.Response.WriteAsync(body.ToJsonString(), context.RequestAborted);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Client error {ErrorCode} answered with status {StatusCode}. TraceId: {TraceId}")]
    private static partial void LogClientError(ILogger logger, string errorCode, int statusCode, string traceId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Server failure {ErrorCode} answered with status {StatusCode}; "
            + "dependency status {DependencyStatus}. TraceId: {TraceId}")]
    private static partial void LogServerFailure(
        ILogger logger,
        string errorCode,
        int? dependencyStatus,
        int statusCode,
        string traceId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Unhandled exception answered as {ErrorCode} with status {StatusCode}. TraceId: {TraceId}")]
    private static partial void LogUnhandledException(
        ILogger logger,
        Exception exception,
        string errorCode,
        int statusCode,
        string traceId);
}
