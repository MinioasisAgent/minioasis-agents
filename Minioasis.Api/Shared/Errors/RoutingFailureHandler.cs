using Minioasis.Application.Shared.Errors;

namespace Minioasis.Api.Shared.Errors;

/// <summary>
/// Gives the framework's empty 404 (no route matches) and 405 (the route exists, not for this method) the one
/// error shape. The <c>Allow</c> header that the framework set is kept.
/// </summary>
internal sealed class RoutingFailureHandler(ProblemResponseFactory problems)
{
    private static readonly IReadOnlyDictionary<string, string> NoMetadata = new Dictionary<string, string>();

    public async Task HandleAsync(HttpContext context)
    {
        var definition = context.Response.StatusCode switch
        {
            StatusCodes.Status404NotFound => RoutingErrorDefinitions.NotFound,
            StatusCodes.Status405MethodNotAllowed => RoutingErrorDefinitions.MethodNotAllowed,
            _ => null,
        };

        if (definition is not null)
        {
            var error = new ApplicationError(definition, Operation: "routing", NoMetadata);
            await problems.Create(error, context).ExecuteAsync(context);
        }
    }
}
