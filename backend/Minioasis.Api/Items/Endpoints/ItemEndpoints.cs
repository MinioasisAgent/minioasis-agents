using Minioasis.Api.Shared.Errors;
using Minioasis.Application.Items.Requests;
using Minioasis.Application.Items.UseCases;

namespace Minioasis.Api.Items.Endpoints;

internal static class ItemEndpoints
{
    public static IEndpointRouteBuilder MapItemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/items/{id}", GetItemAsync);
        return endpoints;
    }

    private static async Task<IResult> GetItemAsync(
        string id,
        IGetItemUseCase useCase,
        ApplicationResultHttpMapper mapper,
        HttpContext context,
        CancellationToken cancellationToken) =>
        mapper.Map(await useCase.ExecuteAsync(new GetItemRequest(id), cancellationToken), context);
}
