namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// The error logged for a failure that nothing anticipated, such as an unhandled exception.
/// </summary>
public static class SystemErrorDefinitions
{
    public static readonly ApplicationErrorDefinition Unexpected =
        new("system.unexpected", ApplicationErrorCategory.Unexpected);
}
