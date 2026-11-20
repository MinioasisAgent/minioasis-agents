namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// Marks a static class whose public static <see cref="ApplicationErrorDefinition"/> fields are error definitions.
/// Every marked class is collected into the registry, so that no definition escapes the startup check.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ErrorDefinitionsAttribute : Attribute
{
}
