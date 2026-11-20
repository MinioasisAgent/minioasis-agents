using Microsoft.AspNetCore.Http.Features;
using Minioasis.Api.Shared.Models;
using System.Text.Json.Nodes;

namespace Minioasis.Api.Shared.Factories;

/// <summary>
/// The only builder of error bodies (ER-005): client-visible (R2) and masked (R3).
/// </summary>
public static class ProblemResponseFactory
{
    public const string ContentType = "application/problem+json";

    private const string MaskedTitle = "Internal error";
    private const string MaskedCode = "internal_error";

    /// <summary>
    /// The W3C identifier of the request's activity (R4). The activity always exists, because Api listens to
    /// ASP.NET's activity source (<c>RequestActivityListenerHostedService</c>).
    /// </summary>
    /// <exception cref="InvalidOperationException">The request has no activity.</exception>
    public static string GetTraceId(HttpContext context) =>
        context.Features.Get<IHttpActivityFeature>()?.Activity.Id
            ?? throw new InvalidOperationException("The request has no activity, so it has no trace identifier.");

    /// <summary>The body of a client-visible error (R2): status, title, detail, code, traceId.</summary>
    public static JsonObject CreateClientVisible(int status, string code, ErrorMessage message, string traceId) =>
        new()
        {
            ["status"] = status,
            ["title"] = message.Title,
            ["detail"] = message.Detail,
            ["code"] = code,
            ["traceId"] = traceId,
        };

    /// <summary>
    /// The body of every server failure (R3, ER-006): status, title, code, traceId, and nothing else.
    /// </summary>
    public static JsonObject CreateMasked(int status, string traceId) =>
        new()
        {
            ["status"] = status,
            ["title"] = MaskedTitle,
            ["code"] = MaskedCode,
            ["traceId"] = traceId,
        };
}
