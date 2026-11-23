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

    public static TheoryData<string, ApplicationErrorDefinition?[]> InconsistentRegistries => new()
    {
        { "empty registry", [] },
        { "null definition", [Visible, null] },
        { "blank code", [Visible, new ApplicationErrorDefinition(" ", ApplicationErrorCategory.DependencyUnavailable)] },
        { "empty code", [Visible, new ApplicationErrorDefinition("", ApplicationErrorCategory.DependencyUnavailable)] },
        { "duplicate code", [Visible, Internal, new ApplicationErrorDefinition("things.down", ApplicationErrorCategory.Unexpected)] },
        { "invalid category", [Visible, new ApplicationErrorDefinition("things.odd", (ApplicationErrorCategory)99)] },
        { "zero category", [Visible, new ApplicationErrorDefinition("things.odd", default)] },
    };

    [Theory]
    [MemberData(nameof(InconsistentRegistries))]
    public void Refuses_an_inconsistent_registry(string name, ApplicationErrorDefinition?[] definitions)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ErrorMessageCatalog.Load(Registry(definitions), [Teapot], ValidJson));
        Assert.False(string.IsNullOrWhiteSpace(exception.Message), name);
    }

    /// <summary>Keyed by case name, because a test method cannot take Api's internal type as a parameter.</summary>
    private static readonly Dictionary<string, HttpErrorDefinition?[]> InconsistentErrorsOfHttp = new()
    {
        ["null definition"] = [Teapot, null],
        ["blank code"] = [Teapot, new HttpErrorDefinition(" ", 404)],
        ["empty code"] = [Teapot, new HttpErrorDefinition("", 404)],
        ["duplicate code"] = [Teapot, new HttpErrorDefinition("teapot", 404)],
        ["code of an Application definition"] = [Teapot, new HttpErrorDefinition("things.not_found", 404)],
        ["server status"] = [Teapot with { Status = 503 }],
        ["success status"] = [Teapot with { Status = 200 }],
        ["status below 400"] = [Teapot with { Status = 399 }],
        ["status above 499"] = [Teapot with { Status = 500 }],
        ["without a message"] = [Teapot, new HttpErrorDefinition("gone", 410)],
    };

    public static TheoryData<string> InconsistentErrorsOfHttpCases => [.. InconsistentErrorsOfHttp.Keys];

    [Theory]
    [MemberData(nameof(InconsistentErrorsOfHttpCases))]
    public void Refuses_an_inconsistent_error_of_http(string name)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ErrorMessageCatalog.Load(Registry(Visible, Internal), InconsistentErrorsOfHttp[name], ValidJson));
        Assert.False(string.IsNullOrWhiteSpace(exception.Message), name);
    }

    public static TheoryData<string, string> InconsistentMessageFiles => new()
    {
        { "client-visible definition without a message", "{}" },
        { "message for an internal error", """{ "things.not_found": { "title": "T", "detail": "D" }, "things.down": { "title": "T", "detail": "D" } }""" },
        { "unknown message", """{ "things.not_found": { "title": "T", "detail": "D" }, "things.unknown": { "title": "T", "detail": "D" } }""" },
        { "duplicate message", """{ "things.not_found": { "title": "T", "detail": "D" }, "things.not_found": { "title": "T", "detail": "D" } }""" },
        { "null message", """{ "things.not_found": null }""" },
        { "malformed file", """{ "things.not_found": { "title": "T", "detail": "D" }""" },
        { "file that is not an object", """[ { "title": "T", "detail": "D" } ]""" },
        { "message that is not an object", """{ "things.not_found": "Thing not found" }""" },
        { "title that is not a string", """{ "things.not_found": { "title": 1, "detail": "D" } }""" },
        { "detail that is not a string", """{ "things.not_found": { "title": "T", "detail": ["D"] } }""" },
        { "message with an unknown field", """{ "things.not_found": { "title": "T", "detail": "D", "type": "x" } }""" },
        { "missing title", """{ "things.not_found": { "detail": "D" } }""" },
        { "missing detail", """{ "things.not_found": { "title": "T" } }""" },
        { "blank title", """{ "things.not_found": { "title": " ", "detail": "D" } }""" },
        { "blank detail", """{ "things.not_found": { "title": "T", "detail": "" } }""" },
        { "null title", """{ "things.not_found": { "title": null, "detail": "D" } }""" },
        { "empty file", "" },
    };

    [Theory]
    [MemberData(nameof(InconsistentMessageFiles))]
    public void Refuses_an_inconsistent_message_file(string name, string json)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ErrorMessageCatalog.Load(Registry(Visible, Internal), [], json));
        Assert.False(string.IsNullOrWhiteSpace(exception.Message), name);
    }

    private static ApplicationErrorRegistry Registry(params ApplicationErrorDefinition?[] definitions) => new(definitions);
}
