using Xunit;

namespace Minioasis.Api.Tests.Shared.TestDoubles;

/// <summary>
/// A clock that moves only when the test advances it, and fires the timers that fall due (TS-007).
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private readonly Lock gate = new();
    private readonly List<FakeTimer> timers = [];
    private DateTimeOffset now = new(2026, 11, 23, 8, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        lock (gate)
        {
            return now;
        }
    }

    /// <summary>The number of timers that are waiting to fire.</summary>
    public int PendingTimers
    {
        get
        {
            lock (gate)
            {
                return timers.Count(timer => timer.DueAt is not null);
            }
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FakeTimer(this, callback, state);
        lock (gate)
        {
            timers.Add(timer);
        }

        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        List<FakeTimer> due;
        lock (gate)
        {
            now += by;
            due = [.. timers.Where(timer => timer.DueAt <= now)];
            foreach (var timer in due)
            {
                timer.Reschedule();
            }
        }

        foreach (var timer in due)
        {
            timer.Fire();
        }
    }

    /// <summary>Waits, for at most five seconds, until at least <paramref name="count"/> timers are pending.</summary>
    public async Task WaitForPendingTimersAsync(int count)
    {
        var limit = DateTime.UtcNow.AddSeconds(5);
        while (PendingTimers < count)
        {
            if (DateTime.UtcNow > limit)
            {
                throw new TimeoutException($"Expected {count} pending timers, found {PendingTimers}.");
            }

            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    private void Remove(FakeTimer timer)
    {
        lock (gate)
        {
            timers.Remove(timer);
        }
    }

    private sealed class FakeTimer(FakeTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan period = Timeout.InfiniteTimeSpan;

        public DateTimeOffset? DueAt
        {
            get; private set;
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner.gate)
            {
                this.period = period;
                DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner.now + dueTime;
            }

            if (dueTime == TimeSpan.Zero)
            {
                owner.Advance(TimeSpan.Zero);
            }

            return true;
        }

        public void Reschedule() =>
            DueAt = period == Timeout.InfiniteTimeSpan || period == TimeSpan.Zero ? null : DueAt + period;

        public void Fire() => callback(state);

        public void Dispose() => owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
