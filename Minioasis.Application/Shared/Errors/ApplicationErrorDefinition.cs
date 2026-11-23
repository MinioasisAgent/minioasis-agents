namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// The one definition of an error: a stable code and a category (ER-002).
/// </summary>
public sealed record ApplicationErrorDefinition(string Code, ApplicationErrorCategory Category)
{
    /// <summary>
    /// True when the client can act on the error, so that it has a public code and a message (ER-003).
    /// Every other error is a server failure and is masked (ER-006).
    /// </summary>
    public bool IsClientVisible => Category is ApplicationErrorCategory.Validation
        or ApplicationErrorCategory.NotFound
        or ApplicationErrorCategory.MethodNotAllowed;
}
