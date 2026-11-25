namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// Marks an error-definition group, so that <see cref="ApplicationErrorRegistry"/> collects its definitions (ER-002).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ErrorDefinitionsAttribute : Attribute;
