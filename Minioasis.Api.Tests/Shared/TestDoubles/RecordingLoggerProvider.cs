using Microsoft.Extensions.Logging;

namespace Minioasis.Api.Tests.Shared.TestDoubles;

/// <summary>
/// Captures every entry of every category at every level, for one test host.
/// </summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    private const string HostingCategory = "Microsoft.AspNetCore.Hosting.Diagnostics";
    private const int RequestFinishedEventId = 2;

    private readonly Lock gate = new();
    private readonly List<LogEntry> entries = [];
    private readonly List<(Func<LogEntry, bool> Predicate, TaskCompletionSource<LogEntry> Match)> waiters = [];

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

    /// <summary>The entries written by Minioasis code (spec 001, "Error responses").</summary>
    public IReadOnlyList<LogEntry> MinioasisEntries =>
        [.. Entries.Where(entry => entry.Category.StartsWith("Minioasis", StringComparison.Ordinal))];

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(this, categoryName);

    /// <summary>Completes with the first entry, already written or still to come, that matches.</summary>
    public Task<LogEntry> WaitForAsync(Func<LogEntry, bool> predicate)
    {
        lock (gate)
        {
            var existing = entries.FirstOrDefault(predicate);
            if (existing is not null)
            {
                return Task.FromResult(existing);
            }

            var match = new TaskCompletionSource<LogEntry>(TaskCreationOptions.RunContinuationsAsynchronously);
            waiters.Add((predicate, match));
            return match.Task;
        }
    }

    /// <summary>
    /// Completes when ASP.NET has logged the end of a request, the last entry it writes for one; for a host that
    /// serves a single request, every entry of that request is then recorded.
    /// </summary>
    public Task WaitForRequestFinishedAsync() =>
        WaitForAsync(entry => entry.Category == HostingCategory && entry.EventId.Id == RequestFinishedEventId)
            .WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

    public void Dispose()
    {
    }

    private void Add(LogEntry entry)
    {
        lock (gate)
        {
            entries.Add(entry);
            foreach (var waiter in waiters.Where(waiter => waiter.Predicate(entry)).ToList())
            {
                waiters.Remove(waiter);
                waiter.Match.TrySetResult(entry);
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
            where TState : notnull => null;

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
