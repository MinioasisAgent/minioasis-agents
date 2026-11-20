using Microsoft.Extensions.Logging;
using Minioasis.Api.Tests.Shared.Factories;
using Xunit;

namespace Minioasis.Api.Tests.Shared.Extensions;

public sealed class ApiErrorHandlingTests
{
    /// <summary>
    /// A framework guard (R22, Api part): ASP.NET answers a caller's abort itself and never calls the exception
    /// handler, so nothing in Minioasis turns it into a failure or an <c>Error</c> entry.
    /// </summary>
    [Fact]
    public async Task Aborted_request_logs_no_error()
    {
        await using var factory = new ApiTestHostFactory();
        using var client = factory.CreateClient();
        using var abort = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var pending = client.GetAsync("/test/wait", abort.Token);
        await factory.TestEndpoints.WaitStarted
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await abort.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        await factory.Logs.WaitForRequestFinishedAsync();
        Assert.DoesNotContain(factory.Logs.Entries, entry => entry.Level >= LogLevel.Error);
        Assert.Empty(factory.Logs.MinioasisEntries);
    }
}
