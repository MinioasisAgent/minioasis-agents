using Minioasis.Koha.Shared.Options;

namespace Minioasis.Koha.Shared.Handlers;

/// <summary>
/// Puts one deadline on everything below it: the token, the request, the retry, and reading the body (EX-011).
/// </summary>
/// <remarks>
/// When the deadline expires, it throws <see cref="TimeoutException"/>: a dependency failure, not a cancellation.
/// When the caller cancels, whatever failed below is reported as <see cref="OperationCanceledException"/> (ER-009).
/// </remarks>
internal sealed class KohaDeadlineHandler(TimeProvider timeProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(KohaOptions.Deadline, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);

        HttpResponseMessage? response = null;
        try
        {
            response = await base.SendAsync(request, linked.Token);
            await response.Content.LoadIntoBufferAsync(linked.Token);
            return response;
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            response?.Dispose();
            throw new OperationCanceledException("The caller cancelled the Koha request.", exception, cancellationToken);
        }
        catch (OperationCanceledException exception) when (deadline.IsCancellationRequested)
        {
            response?.Dispose();
            throw new TimeoutException("The Koha request did not finish within its deadline.", exception);
        }
        catch
        {
            response?.Dispose();
            throw;
        }
    }
}
