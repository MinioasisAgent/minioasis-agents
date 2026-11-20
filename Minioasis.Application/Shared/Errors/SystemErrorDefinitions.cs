namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// Errors of the application itself.
/// </summary>
[ErrorDefinitions]
public static class SystemErrorDefinitions
{
    /// <summary>An unexpected failure, including an unhandled exception. Masked (R3).</summary>
    public static readonly ApplicationErrorDefinition Unexpected =
        new("system.unexpected", ApplicationErrorCategory.Unexpected);
}
