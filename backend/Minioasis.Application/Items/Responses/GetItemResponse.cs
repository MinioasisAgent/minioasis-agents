namespace Minioasis.Application.Items.Responses;

/// <summary>
/// One Koha item. The identifiers are decimal strings.
/// </summary>
public sealed record GetItemResponse(string Id, string BibliographicRecordId, string? Barcode, string? CallNumber);
