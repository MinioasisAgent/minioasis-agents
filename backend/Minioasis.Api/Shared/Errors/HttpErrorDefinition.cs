namespace Minioasis.Api.Shared.Errors;

/// <summary>
/// The one definition of an error of HTTP itself, such as a path that matches no route: its public code and its
/// status (ER-004). Application knows nothing of these (AR-003). Every one is client-visible, so it has a message
/// in <c>Resources/errors.en.json</c> (ER-011).
/// </summary>
internal sealed record HttpErrorDefinition(string Code, int Status);
