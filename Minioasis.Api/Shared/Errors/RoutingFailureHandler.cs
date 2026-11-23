namespace Minioasis.Api.Shared.Errors;

/// <summary>
/// Gives the framework's empty 404 (no route matches) and 405 (the route exists, not for this method) the one
/// error shape. The <c>Allow</c> header that the framework set is kept.
/// </summary>
internal sealed class RoutingFailureHandler(ProblemResponseFactory problems)
{
    public async Task HandleAsync(HttpContext context)
    {
        var error = HttpErrorDefinitions.All.FirstOrDefault(error => error.Status == context.Response.StatusCode);

        if (error is not null)
        {
            await problems.Create(error, context).ExecuteAsync(context);
        }
    }
}
