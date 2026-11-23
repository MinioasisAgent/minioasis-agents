namespace Minioasis.Application.Items.Requests;

/// <summary>
/// The input to get item: the item number as the client sent it, not yet validated.
/// </summary>
public sealed record GetItemRequest(string? Id);
