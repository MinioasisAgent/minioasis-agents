namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// One occurrence of an error: its definition, the operation in which it happened, and the values for the
/// placeholders of its message.
/// </summary>
public sealed record ApplicationError(
    ApplicationErrorDefinition Definition,
    string Operation,
    IReadOnlyDictionary<string, string> Metadata);
