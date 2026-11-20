using Microsoft.AspNetCore.Diagnostics;
using Minioasis.Api.Shared.Mappers;
using Minioasis.Application.Shared.Errors;

namespace Minioasis.Api.Shared.Handlers;

/// <summary>
/// Gives the empty error responses that ASP.NET produces before Minioasis code runs the Problem Details shape
/// (D2, R6 to R8a): 404 is <c>not_found</c>, 405 is <c>method_not_allowed</c>, and 400, 413, and 415 are
/// <c>request.invalid</c>, answered with 400. Every other status is left unchanged.
/// </summary>
public sealed class StatusCodeProblemHandler(ApplicationResultHttpMapper mapper)
{
    public Task HandleAsync(StatusCodeContext context)
    {
        var definition = context.HttpContext.Response.StatusCode switch
        {
            StatusCodes.Status404NotFound => RequestErrorDefinitions.NotFound,
            StatusCodes.Status405MethodNotAllowed => RequestErrorDefinitions.MethodNotAllowed,
            StatusCodes.Status400BadRequest
                or StatusCodes.Status413PayloadTooLarge
                or StatusCodes.Status415UnsupportedMediaType => RequestErrorDefinitions.Invalid,
            _ => null,
        };

        return definition is null
            ? Task.CompletedTask
            : mapper.MapError(new ApplicationError(definition)).ExecuteAsync(context.HttpContext);
    }
}
