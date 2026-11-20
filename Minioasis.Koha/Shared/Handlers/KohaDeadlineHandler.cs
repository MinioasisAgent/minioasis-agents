using Minioasis.Koha.Shared.Exceptions;
using Minioasis.Koha.Shared.Options;

namespace Minioasis.Koha.Shared.Handlers;

/// <summary>
/// Puts one deadline, <see cref="KohaOptions.Deadline"/>, on everything beneath it in a Koha client: from the moment
/// a request is sent, until its whole response body has been read (spec 001, R21; <c>EX-011</c>). On the API client
/// it is the outermost handler, so the deadline also covers getting the token and the retry after a 401; on the
/// OAuth client it bounds the shared token request on its own (R18). It is the only place that applies the deadline
/// (<c>CD-006</c>).
/// </summary>
/// <remarks>
/// When the deadline expires, the result is a dependency failure, <see cref="KohaUnavailableException"/>, not a
/// cancellation (<c>ER-009</c>). The caller's own cancellation propagates as a cancellation, also when it coincides
/// with the expiry (R22).
/// </remarks>
public sealed class KohaDeadlineHandler : DelegatingHandler
{
    private readonly KohaOptions options;
    private readonly TimeProvider timeProvider;

    public KohaDeadlineHandler(KohaOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.options = options;
        this.timeProvider = timeProvider;
    }

    /// <exception cref="KohaUnavailableException">The deadline expired before the body was read in full.</exception>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // The deadline starts now, when the request is sent, not when the handler was made (R18).
        using var deadline = new CancellationTokenSource(options.Deadline, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            var response = await base.SendAsync(request, linked.Token).ConfigureAwait(false);
            try
            {
                // The body is read here, inside the deadline, so that a slow body cannot outlive it (EX-011).
                await response.Content.LoadIntoBufferAsync(linked.Token).ConfigureAwait(false);
                return response;
            }
            catch
            {
                response.Dispose();
                throw;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Only the deadline cancels beneath this handler besides the caller (ER-009). Not wrapped: the
            // cancellation says nothing more about Koha, and Koha's body is never carried (R23).
            throw new KohaUnavailableException();
        }
    }
}
