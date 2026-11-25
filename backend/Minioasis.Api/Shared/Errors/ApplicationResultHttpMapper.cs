using Minioasis.Application.Shared.Results;

namespace Minioasis.Api.Shared.Errors;

/// <summary>
/// The one mapper from a use case's result to HTTP (AR-004). A server failure is logged with its internal code,
/// its operation, and the trace identifier of the response (ER-007).
/// </summary>
internal sealed partial class ApplicationResultHttpMapper(
    ProblemResponseFactory problems,
    ILogger<ApplicationResultHttpMapper> logger)
{
    public IResult Map<TResponse>(ApplicationResult<TResponse> result, HttpContext context)
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        if (!result.Error.Definition.IsClientVisible)
        {
            LogServerFailure(result.Error.Definition.Code, result.Error.Operation, ProblemResponseFactory.TraceIdOf(context));
        }

        return problems.Create(result.Error, context);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Application failure {ErrorCode} in {Operation}. TraceId: {TraceId}")]
    private partial void LogServerFailure(string errorCode, string operation, string traceId);
}
