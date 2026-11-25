using Microsoft.Extensions.Logging;

namespace Minioasis.Api.Tests.Shared.TestDoubles;

internal sealed record LogEntry(
    string Category,
    LogLevel Level,
    string Message,
    IReadOnlyDictionary<string, object?> Values,
    Exception? Exception);
