using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Minioasis.Api.Authentication.Options;
using Minioasis.Api.Shared.Errors;
using Minioasis.Application.Shared.Errors;
using System.Security.Claims;

namespace Minioasis.Api.Authentication.Handlers;

/// <summary>
/// The backend's own boundary in front of routing (brief 002): every request, on every path, must carry a Cloudflare
/// Access token for the configured email, and <c>X-Requested-With: XMLHttpRequest</c>. The checks run in the order of
/// part 3: the signing keys, the token, the email, the header. Only then does routing see the request.
/// </summary>
internal sealed partial class StaffAccessHandler(
    RequestDelegate next,
    IConfigurationManager<OpenIdConnectConfiguration> signingKeys,
    IOptions<CloudflareAccessOptions> options,
    ProblemResponseFactory problems,
    ILogger<StaffAccessHandler> logger)
{
    private const string RequestedWithHeader = "X-Requested-With";
    private const string RequestedWithValue = "XMLHttpRequest";
    private const string EmailClaim = "email";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!await HoldsSigningKeysAsync(context))
        {
            await AnswerKeysUnavailableAsync(context);
            return;
        }

        var authentication = await context.AuthenticateAsync(StaffAccessExtensions.AuthenticationScheme);
        if (!authentication.Succeeded)
        {
            await context.ChallengeAsync(StaffAccessExtensions.AuthenticationScheme);
            await RefuseAsync(context, HttpErrorDefinitions.Unauthenticated);
            return;
        }

        if (!HasConfiguredEmail(authentication.Principal))
        {
            await RefuseAsync(context, HttpErrorDefinitions.AccessDenied);
            return;
        }

        if (!CameFromMinioasis(context.Request))
        {
            await RefuseAsync(context, HttpErrorDefinitions.RequestRefused);
            return;
        }

        await next(context);
    }

    /// <summary>
    /// Asks the configuration manager for the keys. It answers from the keys it holds, or fetches them, and fails only
    /// when it holds none and the fetch fails. A caller who leaves while waiting is a cancellation, not a failure (ER-009).
    /// </summary>
    private async Task<bool> HoldsSigningKeysAsync(HttpContext context)
    {
        try
        {
            await signingKeys.GetConfigurationAsync(context.RequestAborted);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (context.RequestAborted.IsCancellationRequested)
            {
                throw new OperationCanceledException("The caller left while the keys were fetched.", exception, context.RequestAborted);
            }

            return false;
        }
    }

    private bool HasConfiguredEmail(ClaimsPrincipal principal)
    {
        var emails = principal.FindAll(EmailClaim).ToList();
        return emails.Count == 1
            && string.Equals(emails[0].Value, options.Value.Email, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CameFromMinioasis(HttpRequest request)
    {
        var values = request.Headers[RequestedWithHeader];
        return values.Count == 1 && string.Equals(values[0], RequestedWithValue, StringComparison.Ordinal);
    }

    /// <summary>The masked 503 of a server failure (ER-006), with the internal code logged beside the trace identifier (ER-007).</summary>
    private Task AnswerKeysUnavailableAsync(HttpContext context)
    {
        var failure = AccessErrorDefinitions.KeysUnavailable;
        LogKeysUnavailable(failure.Code, ProblemResponseFactory.TraceIdOf(context));
        return ProblemResponseFactory
            .CreateMasked(ProblemResponseFactory.StatusFor(failure.Category), context)
            .ExecuteAsync(context);
    }

    private Task RefuseAsync(HttpContext context, HttpErrorDefinition refusal)
    {
        LogRefused(refusal.Code, ProblemResponseFactory.TraceIdOf(context));
        return problems.Create(refusal, context).ExecuteAsync(context);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Access failure {ErrorCode}. TraceId: {TraceId}")]
    private partial void LogKeysUnavailable(string errorCode, string traceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Request refused with {ErrorCode}. TraceId: {TraceId}")]
    private partial void LogRefused(string errorCode, string traceId);
}
