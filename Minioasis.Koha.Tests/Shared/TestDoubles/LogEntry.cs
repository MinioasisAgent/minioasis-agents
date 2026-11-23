using Microsoft.Extensions.Logging;

namespace Minioasis.Koha.Tests.Shared.TestDoubles;

internal sealed record LogEntry(
    string Category,
    LogLevel Level,
    string Message,
    IReadOnlyDictionary<string, object?> Values,
    Exception? Exception)
{
    /// <summary>Everything the entry would write: the message, each value, and the exception.</summary>
    public string AllText =>
        string.Join('\n', [Message, .. Values.Select(pair => $"{pair.Key}={pair.Value}"), Exception?.ToString()]);
}
