using System.Diagnostics;

namespace Minioasis.Api.Shared.Hosting;

/// <summary>
/// Listens to ASP.NET's activity source for as long as the host runs, so that every request has a W3C activity,
/// and so a <c>traceId</c>, even when logging is switched off (R4). ASP.NET creates the activity only when something
/// listens.
/// </summary>
public sealed class RequestActivityListenerHostedService : IHostedService, IDisposable
{
    private const string AspNetCoreActivitySource = "Microsoft.AspNetCore";

    private ActivityListener? listener;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AspNetCoreActivitySource,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.PropagationData,
        };
        ActivitySource.AddActivityListener(listener);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        listener?.Dispose();
        listener = null;
    }
}
