using Minioasis.Application.Shared.Errors;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Minioasis.Api.Shared.Errors;

/// <summary>
/// Builds every error response of the API (ER-005), decides the status of an Application error from its category
/// (ER-004), and masks every server failure (ER-006).
/// </summary>
internal sealed partial class ProblemResponseFactory(ErrorMessageCatalog messages)
{
    public const string ContentType = "application/problem+json";
    public const string MaskedCode = "internal_error";
    public const string MaskedTitle = "Internal error";

    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    /// <summary>The status of each category: the table of ER-004.</summary>
    public static int StatusFor(ApplicationErrorCategory category) => category switch
    {
        ApplicationErrorCategory.Validation => StatusCodes.Status400BadRequest,
        ApplicationErrorCategory.NotFound => StatusCodes.Status404NotFound,
        ApplicationErrorCategory.DependencyInvalidResponse => StatusCodes.Status502BadGateway,
        ApplicationErrorCategory.DependencyUnavailable => StatusCodes.Status503ServiceUnavailable,
        ApplicationErrorCategory.DependencyAuthenticationFailed => StatusCodes.Status500InternalServerError,
        ApplicationErrorCategory.Unexpected => StatusCodes.Status500InternalServerError,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "The category has no status."),
    };

    /// <summary>The trace identifier that the response carries and the log repeats (ER-007).</summary>
    public static string TraceIdOf(HttpContext context) => Activity.Current?.Id ?? context.TraceIdentifier;

    /// <summary>
    /// The response for an error: its own text when the client can act on it, otherwise the masked response.
    /// </summary>
    public IResult Create(ApplicationError error, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(error);

        var status = StatusFor(error.Definition.Category);
        if (!error.Definition.IsClientVisible)
        {
            return CreateMasked(status, context);
        }

        var message = messages.Get(error.Definition.Code);
        return Write(new ProblemResponse(
            status,
            message.Title,
            FillPlaceholders(message.Detail, error.Metadata),
            error.Definition.Code,
            TraceIdOf(context)));
    }

    /// <summary>The response for an error of HTTP itself: its own status, title, and detail.</summary>
    public IResult Create(HttpErrorDefinition error, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(error);

        var message = messages.Get(error.Code);
        return Write(new ProblemResponse(
            error.Status,
            message.Title,
            FillPlaceholders(message.Detail, NoValues),
            error.Code,
            TraceIdOf(context)));
    }

    /// <summary>The one masked response for a server failure: no detail, only the status and the trace identifier.</summary>
    public static IResult CreateMasked(int status, HttpContext context) =>
        Write(new ProblemResponse(status, MaskedTitle, Detail: null, MaskedCode, TraceIdOf(context)));

    private static IResult Write(ProblemResponse body) =>
        Results.Json(body, options: null, contentType: ContentType, statusCode: body.Status);

    private static string FillPlaceholders(string text, IReadOnlyDictionary<string, string> values) =>
        Placeholder().Replace(text, match => values.TryGetValue(match.Groups[1].Value, out var value)
            ? value
            : throw new InvalidOperationException($"No value was supplied for the placeholder {match.Value}."));

    [GeneratedRegex(@"\{([A-Za-z0-9_]+)\}")]
    private static partial Regex Placeholder();
}
