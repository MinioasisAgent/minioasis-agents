using Microsoft.Extensions.Logging;

namespace Minioasis.Koha.Tests.Shared.TestDoubles;

/// <summary>
/// Captures every entry and every scope, of every category, at every level (spec 001, R23: "every category,
/// including <c>HttpClient</c>'s").
/// </summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly Lock gate = new();
    private readonly List<LogEntry> entries = [];
    private readonly List<string> scopes = [];

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (gate)
            {
                return [.. entries];
            }
        }
    }

    /// <summary>
    /// Every text that was logged: each entry's message, exception text, and property values, and each scope's text
    /// and property values.
    /// </summary>
    public IReadOnlyList<string> Texts
    {
        get
        {
            lock (gate)
            {
                return
                [
                    .. entries.SelectMany(entry => entry.Properties.Values
                        .Select(value => value?.ToString() ?? string.Empty)
                        .Append(entry.Message)
                        .Append(entry.Exception?.ToString() ?? string.Empty)),
                    .. scopes,
                ];
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void Add(LogEntry entry)
    {
        lock (gate)
        {
            entries.Add(entry);
        }
    }

    private void AddScope(object state)
    {
        lock (gate)
        {
            scopes.Add(state.ToString() ?? string.Empty);
            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                scopes.AddRange(pairs.Select(pair => pair.Value?.ToString() ?? string.Empty));
            }
        }
    }

    public sealed record LogEntry(
        string Category,
        LogLevel Level,
        EventId EventId,
        string Message,
        Exception? Exception,
        IReadOnlyDictionary<string, object?> Properties);

    private sealed class RecordingLogger(RecordingLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            provider.AddScope(state);
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                foreach (var (key, value) in pairs)
                {
                    properties[key] = value;
                }
            }

            provider.Add(new LogEntry(category, logLevel, eventId, formatter(state, exception), exception, properties));
        }
    }
}
