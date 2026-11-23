using Microsoft.AspNetCore.Diagnostics;
using Minioasis.Application.Shared.Errors;

namespace Minioasis.Api.Shared.Errors;

/// <summary>
/// Answers an unhandled exception with the masked 500 and logs the exception with the trace identifier of the
/// response (ER-006, ER-007). The exception handler middleware does not call it when the caller abandoned the
/// request (ER-009).
/// </summary>
internal sealed partial class UnhandledExceptionHandler(ILogger<UnhandledExceptionHandler> logger)
{
    public async Task HandleAsync(HttpContext context)
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var traceId = ProblemResponseFactory.TraceIdOf(context);
        LogUnhandled(exception, SystemErrorDefinitions.Unexpected.Code, traceId);

        await ProblemResponseFactory.CreateMasked(StatusCodes.Status500InternalServerError, context).ExecuteAsync(context);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled failure {ErrorCode}. TraceId: {TraceId}")]
    private partial void LogUnhandled(Exception? exception, string errorCode, string traceId);
}
