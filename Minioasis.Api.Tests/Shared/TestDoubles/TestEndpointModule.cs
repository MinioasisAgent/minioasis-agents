using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Minioasis.Api.Shared.Abstractions;
using Minioasis.Api.Shared.Mappers;
using Minioasis.Application.Shared.Errors;
using Minioasis.Application.Shared.Results;
using System.Globalization;

namespace Minioasis.Api.Tests.Shared.TestDoubles;

/// <summary>
/// Test routes for R1 to R8a, registered in the test host as an <see cref="IEndpointModule"/>.
/// </summary>
public sealed class TestEndpointModule : IEndpointModule
{
    /// <summary>The message of the exception that <c>GET /test/throw</c> throws.</summary>
    public const string ExceptionMarker = "test-exception-marker";

    /// <summary>The query name that <c>GET /test/failures/{code}</c> reads as the dependency's status.</summary>
    public const string DependencyStatusQueryName = "dependencyStatus";

    private readonly TaskCompletionSource waitStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when a request to <c>GET /test/wait</c> has reached the endpoint.</summary>
    public Task WaitStarted => waitStarted.Task;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/test/one", () => "one");
        endpoints.MapMethods("/test/two", [HttpMethods.Get, HttpMethods.Post], () => "two");
        endpoints.MapPost("/test/body", (TestBody body) => body.Number.ToString(CultureInfo.InvariantCulture));
        endpoints.MapGet("/test/items/{id:int}", (int id) => id.ToString(CultureInfo.InvariantCulture));
        endpoints.MapGet("/test/convert/{id}", (int id) => id.ToString(CultureInfo.InvariantCulture));
        endpoints.MapGet("/test/query", (int page) => page.ToString(CultureInfo.InvariantCulture));
        endpoints.MapGet("/test/status/{status:int}", (int status) => Results.StatusCode(status));
        endpoints.MapGet("/test/success", (ApplicationResultHttpMapper mapper) =>
            mapper.Map(ApplicationResult<string>.Success("done"), response => Results.Text(response)));
        endpoints.MapGet("/test/failures/{code}", Fail);
        endpoints.MapGet("/test/throw", string () => throw new InvalidOperationException(ExceptionMarker));
        endpoints.MapGet("/test/wait", WaitAsync);

        // Two endpoints for one route on purpose: routing itself throws, before any endpoint runs.
#pragma warning disable ASP0022
        endpoints.MapGet("/test/ambiguous", () => "first");
        endpoints.MapGet("/test/ambiguous", () => "second");
#pragma warning restore ASP0022
    }

    /// <summary>
    /// Fails with the registered definition named by <paramref name="code"/>. Every query value except
    /// <see cref="DependencyStatusQueryName"/> is a placeholder value.
    /// </summary>
    private static IResult Fail(
        string code,
        HttpRequest request,
        ApplicationErrorDefinitionRegistry registry,
        ApplicationResultHttpMapper mapper)
    {
        var definition = registry.Definitions.Single(definition => definition.Code == code);
        var values = request.Query
            .Where(pair => pair.Key != DependencyStatusQueryName)
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);
        int? dependencyStatus = request.Query.TryGetValue(DependencyStatusQueryName, out var status)
            ? int.Parse(status.ToString(), CultureInfo.InvariantCulture)
            : null;

        return mapper.Map(
            ApplicationResult<string>.Failure(new ApplicationError(definition, values, dependencyStatus)),
            response => Results.Text(response));
    }

    private async Task<string> WaitAsync(CancellationToken cancellationToken)
    {
        waitStarted.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return "unreachable";
    }

    public sealed record TestBody(int Number);
}
