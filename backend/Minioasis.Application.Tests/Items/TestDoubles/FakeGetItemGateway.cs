using Minioasis.Application.Items.Abstractions;
using Minioasis.Application.Items.Models;
using Minioasis.Application.Items.Results;

namespace Minioasis.Application.Tests.Items.TestDoubles;

internal sealed class FakeGetItemGateway(Func<ItemId, CancellationToken, Task<GetItemGatewayResult>> answer)
    : IGetItemGateway
{
    public FakeGetItemGateway(GetItemGatewayResult result)
        : this((_, _) => Task.FromResult(result))
    {
    }

    public int Calls
    {
        get; private set;
    }

    public ItemId? LastItemId
    {
        get; private set;
    }

    public CancellationToken LastCancellationToken
    {
        get; private set;
    }

    public Task<GetItemGatewayResult> GetItemAsync(ItemId itemId, CancellationToken cancellationToken)
    {
        Calls++;
        LastItemId = itemId;
        LastCancellationToken = cancellationToken;
        return answer(itemId, cancellationToken);
    }
}
