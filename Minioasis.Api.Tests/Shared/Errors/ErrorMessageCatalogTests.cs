using Minioasis.Api.Shared.Errors;
using Minioasis.Application.Shared.Errors;
using Xunit;

namespace Minioasis.Api.Tests.Shared.Errors;

public sealed class ErrorMessageCatalogTests
{
    private static readonly ApplicationErrorDefinition Visible = new("things.not_found", ApplicationErrorCategory.NotFound);
    private static readonly ApplicationErrorDefinition Internal = new("things.down", ApplicationErrorCategory.DependencyUnavailable);
    private static readonly HttpErrorDefinition Teapot = new("teapot", 418);

    private const string ValidJson = """
        {
          "things.not_found": { "title": "Thing not found", "detail": "No thing {id}." },
          "teapot": { "title": "Teapot", "detail": "I am a teapot." }
        }
        """;

    /// <summary>The messages of the errors of HTTP alone, for a registry that defines nothing.</summary>
    private const string TeapotOnlyJson = """{ "teapot": { "title": "Teapot", "detail": "I am a teapot." } }""";

    [Fact]
    public void Loads_the_messages_of_a_consistent_registry_errors_of_http_and_file()
    {
        var catalog = ErrorMessageCatalog.Load(Registry(Visible, Internal), [Teapot], ValidJson);

        Assert.Equal(new ErrorMessage("Thing not found", "No thing {id}."), catalog.Get("things.not_found"));
        Assert.Equal(new ErrorMessage("Teapot", "I am a teapot."), catalog.Get("teapot"));
    }

    [Fact]
    public void The_shipped_message_file_matches_the_default_registry_and_the_errors_of_http()
    {
        var catalog = ErrorMessageCatalog.Load(
            ApplicationErrorRegistry.Default,
            HttpErrorDefinitions.All,
            ErrorMessageCatalog.ReadEmbeddedMessages());

        Assert.Equal(
            new ErrorMessage("Invalid item identifier", "The item identifier must be a valid positive identifier."),
            catalog.Get("items.invalid_identifier"));
        Assert.Equal(new ErrorMessage("Item not found", "No item was found with identifier {id}."), catalog.Get("items.not_found"));
        Assert.Equal(new ErrorMessage("Not found", "The requested resource does not exist."), catalog.Get("not_found"));
        Assert.Equal(
            new ErrorMessage("Method not allowed", "This resource does not support the request method."),
            catalog.Get("method_not_allowed"));
    }

    /// <summary>
    /// Each case breaks one check and names the problem that check reports, so that a case cannot pass on a problem
    /// that another check reports first.
    /// </summary>
    public static TheoryData<string, ApplicationErrorDefinition?[], string> InconsistentRegistries => new()
    {
        { "empty registry", [], "The registry is empty." },
        { "null definition", [Visible, null], "The registry holds a null definition." },
        {
            "blank code",
            [Visible, new ApplicationErrorDefinition(" ", ApplicationErrorCategory.DependencyUnavailable)],
            "The registry holds a definition with a blank code."
        },
        {
            "empty code",
            [Visible, new ApplicationErrorDefinition("", ApplicationErrorCategory.DependencyUnavailable)],
            "The registry holds a definition with a blank code."
        },
        {
            "duplicate code",
            [Visible, Internal, new ApplicationErrorDefinition("things.down", ApplicationErrorCategory.Unexpected)],
            "The code things.down is defined more than once."
        },
        {
            "invalid category",
            [Visible, new ApplicationErrorDefinition("things.odd", (ApplicationErrorCategory)99)],
            "The definition things.odd has an invalid category."
        },
        {
            "zero category",
            [Visible, new ApplicationErrorDefinition("things.odd", default)],
            "The definition things.odd has an invalid category."
        },
    };

    [Theory]
    [MemberData(nameof(InconsistentRegistries))]
    public void Refuses_an_inconsistent_registry(string name, ApplicationErrorDefinition?[] definitions, string problem)
    {
        var json = definitions.Length == 0 ? TeapotOnlyJson : ValidJson;

        var exception = Assert.Throws<InvalidOperationException>(
            () => ErrorMessageCatalog.Load(Registry(definitions), [Teapot], json));
        Assert.True(exception.Message.Contains(problem, StringComparison.Ordinal), $"{name}: {exception.Message}");
    }

    /// <summary>Keyed by case name, because a test method cannot take Api's internal type as a parameter.</summary>
    private static readonly Dictionary<string, (HttpErrorDefinition?[] Errors, string Problem)> BadErrorsOfHttp =
        new()
        {
            ["null definition"] = ([Teapot, null], "The errors of HTTP itself hold a null definition."),
            ["blank code"] = ([Teapot, new HttpErrorDefinition(" ", 404)], HttpBlankCode),
            ["empty code"] = ([Teapot, new HttpErrorDefinition("", 404)], HttpBlankCode),
            ["duplicate code"] =
                ([Teapot, new HttpErrorDefinition("teapot", 404)], Duplicate("teapot")),
            ["code of an Application definition"] =
                ([Teapot, new HttpErrorDefinition("things.not_found", 404)], Duplicate("things.not_found")),
            ["server status"] = ([Teapot with { Status = 503 }], NotAClientError(503)),
            ["success status"] = ([Teapot with { Status = 200 }], NotAClientError(200)),
            ["status below 400"] = ([Teapot with { Status = 399 }], NotAClientError(399)),
            ["status above 499"] = ([Teapot with { Status = 500 }], NotAClientError(500)),
            ["without a message"] =
                ([Teapot, new HttpErrorDefinition("gone", 410)], "The client-visible error gone has no message."),
        };

    public static TheoryData<string> InconsistentErrorsOfHttpCases => [.. BadErrorsOfHttp.Keys];

    [Theory]
    [MemberData(nameof(InconsistentErrorsOfHttpCases))]
    public void Refuses_an_inconsistent_error_of_http(string name)
    {
        var (errors, problem) = BadErrorsOfHttp[name];

        var exception = Assert.Throws<InvalidOperationException>(
            () => ErrorMessageCatalog.Load(Registry(Visible, Internal), errors, ValidJson));
        Assert.True(exception.Message.Contains(problem, StringComparison.Ordinal), $"{name}: {exception.Message}");
    }

    /// <summary>
    /// A case that breaks one message keeps a valid <c>things.not_found</c> and puts the bad entry under a second key,
    /// so that the required message is never missing and the case's own check is the one that reports.
    /// </summary>
    public static TheoryData<string, string, string> InconsistentMessageFiles => new()
    {
        {
            "client-visible definition without a message",
            "{}",
            "The client-visible error things.not_found has no message."
        },
        {
            "message for an internal error",
            WithSecondMessage(ValidMessage, key: "things.down"),
            "The message things.down belongs to a server failure, which is never shown to a client."
        },
        {
            "unknown message",
            WithSecondMessage(ValidMessage, key: "things.unknown"),
            "The message things.unknown belongs to no defined error."
        },
        {
            "duplicate message",
            WithSecondMessage(ValidMessage, key: "things.not_found"),
            "The message things.not_found appears more than once."
        },
        { "null message", WithSecondMessage("null"), "The message things.other is null." },
        { "malformed file", $$"""{ "things.not_found": {{ValidMessage}}""", "The message file is not valid JSON." },
        { "empty file", "", "The message file is not valid JSON." },
        { "file that is not an object", $"[ {ValidMessage} ]", "The message file is not a JSON object." },
        { "message that is not an object", WithSecondMessage("\"Thing not found\""), Malformed },
        { "message that is an array", WithSecondMessage("""[ "T", "D" ]"""), Malformed },
        { "message with an unknown field", WithSecondMessage("""{ "title": "T", "detail": "D", "x": 1 }"""), Malformed },
        { "title that is not a string", WithSecondMessage("""{ "title": 1, "detail": "D" }"""), Incomplete },
        { "detail that is not a string", WithSecondMessage("""{ "title": "T", "detail": ["D"] }"""), Incomplete },
        { "missing title", WithSecondMessage("""{ "detail": "D" }"""), Incomplete },
        { "missing detail", WithSecondMessage("""{ "title": "T" }"""), Incomplete },
        { "blank title", WithSecondMessage("""{ "title": " ", "detail": "D" }"""), Incomplete },
        { "blank detail", WithSecondMessage("""{ "title": "T", "detail": "" }"""), Incomplete },
        { "null title", WithSecondMessage("""{ "title": null, "detail": "D" }"""), Incomplete },
    };

    [Theory]
    [MemberData(nameof(InconsistentMessageFiles))]
    public void Refuses_an_inconsistent_message_file(string name, string json, string problem)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ErrorMessageCatalog.Load(Registry(Visible, Internal), [], json));
        Assert.True(exception.Message.Contains(problem, StringComparison.Ordinal), $"{name}: {exception.Message}");
    }

    private const string ValidMessage = """{ "title": "T", "detail": "D" }""";

    private const string HttpBlankCode = "The errors of HTTP itself hold a definition with a blank code.";

    private const string Malformed =
        "The message things.other is malformed: it must be an object with only a title and a detail.";

    private const string Incomplete =
        "The message things.other is incomplete: it needs a title and a detail that are not blank.";

    private static string Duplicate(string code) => $"The code {code} is defined more than once.";

    private static string NotAClientError(int status) =>
        $"The error of HTTP itself teapot has the status {status}, which is not a client error.";

    /// <summary>A valid <c>things.not_found</c>, and the given value under a second key.</summary>
    private static string WithSecondMessage(string value, string key = "things.other") =>
        $$"""{ "things.not_found": {{ValidMessage}}, "{{key}}": {{value}} }""";

    private static ApplicationErrorRegistry Registry(params ApplicationErrorDefinition?[] definitions) => new(definitions);
}
