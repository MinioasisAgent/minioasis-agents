namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// The one definition of an error: its stable code and its category (ER-002). The registry checks every
/// definition when the application starts (R9), so this type accepts any values and leaves the check to it.
/// </summary>
public sealed class ApplicationErrorDefinition(string code, ApplicationErrorCategory category)
{
    public string Code { get; } = code;

    public ApplicationErrorCategory Category { get; } = category;

    /// <summary>
    /// Whether the client sees this error's own code and message. Every other error is masked (ER-006).
    /// </summary>
    public bool IsClientVisible =>
        Category is ApplicationErrorCategory.Validation
            or ApplicationErrorCategory.NotFound
            or ApplicationErrorCategory.MethodNotAllowed;
}
