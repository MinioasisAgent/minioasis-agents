namespace Minioasis.Api.Shared.Errors;

/// <summary>
/// Gives the framework's empty 404 (no route matches) and 405 (the route exists, not for this method) the one
/// error shape. The <c>Allow</c> header that the framework set is kept. Only these two are routing failures: the other
/// errors of HTTP itself always come with their body, and two of them share the status 403.
/// </summary>
internal sealed class RoutingFailureHandler(ProblemResponseFactory problems)
{
    private static readonly HttpErrorDefinition[] RoutingFailures =
        [HttpErrorDefinitions.NotFound, HttpErrorDefinitions.MethodNotAllowed];

    public async Task HandleAsync(HttpContext context)
    {
        var error = RoutingFailures.FirstOrDefault(error => error.Status == context.Response.StatusCode);

        if (error is not null)
        {
            await problems.Create(error, context).ExecuteAsync(context);
        }
    }
}
