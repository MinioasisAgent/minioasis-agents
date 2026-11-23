using Xunit;

namespace Minioasis.Koha.Tests.Shared.TestDoubles;

internal static class Wait
{
    /// <summary>The longest that any test waits for something to happen (TS-007).</summary>
    public static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

    /// <summary>Waits until <paramref name="condition"/> holds, and fails the test after <see cref="Limit"/>.</summary>
    public static async Task UntilAsync(Func<bool> condition)
    {
        var limit = DateTime.UtcNow + Limit;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < limit, "The condition did not become true within the limit.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }
}
