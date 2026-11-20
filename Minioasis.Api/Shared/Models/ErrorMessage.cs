namespace Minioasis.Api.Shared.Models;

/// <summary>
/// The English title and detail of a client-visible error.
/// </summary>
public sealed record ErrorMessage(string Title, string Detail);
