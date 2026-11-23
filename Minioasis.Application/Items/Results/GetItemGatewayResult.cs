using Minioasis.Application.Items.Outcomes;
using Minioasis.Application.Items.Responses;
using System.Diagnostics.CodeAnalysis;

namespace Minioasis.Application.Items.Results;

/// <summary>
/// The gateway's answer: the item when it was found, otherwise the outcome.
/// </summary>
public sealed class GetItemGatewayResult
{
    private GetItemGatewayResult(GetItemOutcome outcome, GetItemResponse? item)
    {
        Outcome = outcome;
        Item = item;
    }

    public GetItemOutcome Outcome
    {
        get;
    }

    public GetItemResponse? Item
    {
        get;
    }

    [MemberNotNullWhen(true, nameof(Item))]
    public bool IsFound => Outcome == GetItemOutcome.Found;

    public static GetItemGatewayResult Found(GetItemResponse item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new GetItemGatewayResult(GetItemOutcome.Found, item);
    }

    public static GetItemGatewayResult Failed(GetItemOutcome outcome)
    {
        if (outcome == GetItemOutcome.Found || !Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "A failure needs a failure outcome.");
        }

        return new GetItemGatewayResult(outcome, null);
    }
}
