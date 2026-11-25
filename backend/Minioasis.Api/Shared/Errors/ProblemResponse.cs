using System.Text.Json.Serialization;

namespace Minioasis.Api.Shared.Errors;

/// <summary>
/// The body of every error response: Problem Details with <c>code</c> and <c>traceId</c>, and without
/// <c>type</c> and <c>instance</c> (ER-005).
/// </summary>
internal sealed record ProblemResponse(
    int Status,
    string Title,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Detail,
    string Code,
    string TraceId);
