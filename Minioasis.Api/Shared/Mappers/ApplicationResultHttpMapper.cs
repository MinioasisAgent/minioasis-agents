using Minioasis.Api.Shared.Writers;
using Minioasis.Application.Shared.Errors;
using Minioasis.Application.Shared.Results;

namespace Minioasis.Api.Shared.Mappers;

/// <summary>
/// Maps an Application result to HTTP: an error's category to its status (R1, ER-004), in one place, and the error
/// to the response that <see cref="ErrorResponseWriter"/> writes.
/// </summary>
public sealed class ApplicationResultHttpMapper(ErrorResponseWriter writer)
{
    /// <summary>The one table of status per category (R1, ER-004).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="category"/> is not a defined value.</exception>
    public static int GetStatus(ApplicationErrorCategory category) => category switch
    {
        ApplicationErrorCategory.Validation => StatusCodes.Status400BadRequest,
        ApplicationErrorCategory.NotFound => StatusCodes.Status404NotFound,
        ApplicationErrorCategory.MethodNotAllowed => StatusCodes.Status405MethodNotAllowed,
        ApplicationErrorCategory.DependencyInvalidResponse => StatusCodes.Status502BadGateway,
        ApplicationErrorCategory.DependencyUnavailable => StatusCodes.Status503ServiceUnavailable,
        ApplicationErrorCategory.DependencyAuthenticationFailed => StatusCodes.Status500InternalServerError,
        ApplicationErrorCategory.Unexpected => StatusCodes.Status500InternalServerError,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Undefined error category."),
    };

    /// <summary>
    /// The endpoint's own response for a success, or the error response for a failure.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="result"/> or <paramref name="onSuccess"/> is null.
    /// </exception>
    public IResult Map<TResponse>(ApplicationResult<TResponse> result, Func<TResponse, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess ? onSuccess(result.Response) : MapError(result.Error);
    }

    /// <summary>The error response for <paramref name="error"/>, with the status of its category.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is null.</exception>
    public IResult MapError(ApplicationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new ErrorResult(writer, error, GetStatus(error.Definition.Category));
    }

    private sealed class ErrorResult(ErrorResponseWriter writer, ApplicationError error, int status) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext) => writer.WriteAsync(httpContext, error, status);
    }
}
