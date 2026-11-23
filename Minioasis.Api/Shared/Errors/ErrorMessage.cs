namespace Minioasis.Api.Shared.Errors;

/// <summary>
/// The English text of a client-visible error. The detail may hold placeholders such as <c>{id}</c>.
/// </summary>
internal sealed record ErrorMessage(string Title, string Detail);
