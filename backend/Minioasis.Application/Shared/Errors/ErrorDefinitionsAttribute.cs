namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// Marks an error-definition group, so that <see cref="ApplicationErrorRegistry"/> collects its definitions (ER-002).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class ErrorDefinitionsAttribute : Attribute;
