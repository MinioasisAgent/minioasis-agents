using Minioasis.Application.Items.Models;
using Minioasis.Application.Items.Results;

namespace Minioasis.Application.Items.Abstractions;

/// <summary>
/// Reads one item from the library system.
/// </summary>
public interface IGetItemGateway
{
    /// <summary>
    /// Returns the item, or the outcome that prevented it. Cancellation by the caller is thrown as
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    Task<GetItemGatewayResult> GetItemAsync(ItemId itemId, CancellationToken cancellationToken);
}
