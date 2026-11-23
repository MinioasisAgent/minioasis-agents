using Minioasis.Application.Items.Abstractions;
using Minioasis.Application.Items.Models;
using Minioasis.Application.Items.Results;

namespace Minioasis.Api.Tests.Items.TestDoubles;

internal sealed class FakeGetItemGateway(Func<ItemId, CancellationToken, Task<GetItemGatewayResult>> answer)
    : IGetItemGateway
{
    public FakeGetItemGateway(GetItemGatewayResult result)
        : this((_, _) => Task.FromResult(result))
    {
    }

    public int Calls => calls;

    public ItemId? LastItemId
    {
        get; private set;
    }

    private int calls;

    public Task<GetItemGatewayResult> GetItemAsync(ItemId itemId, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
        LastItemId = itemId;
        return answer(itemId, cancellationToken);
    }
}
