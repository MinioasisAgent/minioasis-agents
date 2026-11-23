using Minioasis.Application.Items.Abstractions;
using Minioasis.Application.Items.Errors;
using Minioasis.Application.Items.Models;
using Minioasis.Application.Items.Outcomes;
using Minioasis.Application.Items.Requests;
using Minioasis.Application.Items.Responses;
using Minioasis.Application.Shared.Errors;
using Minioasis.Application.Shared.Results;
using System.Globalization;

namespace Minioasis.Application.Items.UseCases;

/// <summary>
/// Gets one item by its Koha item number.
/// </summary>
internal sealed class GetItemUseCase(IGetItemGateway gateway) : IGetItemUseCase
{
    /// <summary>The operation name that the log carries with every failure of this use case.</summary>
    public const string Operation = "items.get";

    private static readonly IReadOnlyDictionary<string, string> NoMetadata = new Dictionary<string, string>();

    public async Task<ApplicationResult<GetItemResponse>> ExecuteAsync(
        GetItemRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ItemId.TryCreate(request.Id, out var itemId))
        {
            return Failure(ItemErrorDefinitions.InvalidIdentifier, NoMetadata);
        }

        var result = await gateway.GetItemAsync(itemId, cancellationToken);
        if (result.IsFound)
        {
            return ApplicationResult<GetItemResponse>.Success(result.Item);
        }

        return result.Outcome switch
        {
            GetItemOutcome.NotFound => Failure(ItemErrorDefinitions.NotFound, IdentifierMetadata(itemId)),
            GetItemOutcome.AuthenticationFailed => Failure(KohaErrorDefinitions.AuthenticationFailed, NoMetadata),
            GetItemOutcome.InvalidResponse => Failure(KohaErrorDefinitions.InvalidResponse, NoMetadata),
            GetItemOutcome.Unavailable => Failure(KohaErrorDefinitions.Unavailable, NoMetadata),
            _ => throw new InvalidOperationException($"The gateway returned the unknown outcome {result.Outcome}."),
        };
    }

    private static Dictionary<string, string> IdentifierMetadata(ItemId itemId) =>
        new()
        {
            ["id"] = itemId.Value.ToString(CultureInfo.InvariantCulture)
        };

    private static ApplicationResult<GetItemResponse> Failure(
        ApplicationErrorDefinition definition,
        IReadOnlyDictionary<string, string> metadata) =>
        ApplicationResult<GetItemResponse>.Failure(new ApplicationError(definition, Operation, metadata));
}
