using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Minioasis.Api.Tests.Shared.TestDoubles;

/// <summary>
/// Keeps every log entry, with its structured values, so that a test can assert what was logged.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> entries = new();

    public IReadOnlyList<LogEntry> Entries => [.. entries];

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, entries);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var values = state as IEnumerable<KeyValuePair<string, object?>> ?? [];
            entries.Enqueue(new LogEntry(
                category,
                logLevel,
                formatter(state, exception),
                values.ToDictionary(pair => pair.Key, pair => pair.Value),
                exception));
        }
    }
}
