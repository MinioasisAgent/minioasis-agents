using Minioasis.Api.Shared.Factories;
using Minioasis.Api.Shared.Models;
using Minioasis.Application.Shared.Errors;
using System.Text;
using Xunit;

namespace Minioasis.Api.Tests.Shared.Factories;

public sealed class ErrorMessageCatalogFactoryTests
{
    private const string Visible = "\"test.visible\": { \"title\": \"Visible\", \"detail\": \"Visible detail.\" }";
    private const string OtherVisible = "\"test.other\": { \"title\": \"Other\", \"detail\": \"Other detail.\" }";

    private static readonly ApplicationErrorDefinitionRegistry Registry = new(new ApplicationErrorDefinitionSource(
    [
        new ApplicationErrorDefinition("test.visible", ApplicationErrorCategory.NotFound),
        new ApplicationErrorDefinition("test.other", ApplicationErrorCategory.Validation),
        new ApplicationErrorDefinition("test.server", ApplicationErrorCategory.Unexpected),
    ]));

    public static readonly TheoryData<string, string, string[]> InconsistentMessages = new()
    {
        { "a client-visible definition with no message", $$"""{ {{Visible}} }""", ["test.other"] },
        {
            "a message for a server-failure definition",
            $$"""{ {{Visible}}, {{OtherVisible}}, "test.server": { "title": "S", "detail": "S." } }""",
            ["test.server"]
        },
        {
            "a message for an unknown code",
            $$"""{ {{Visible}}, {{OtherVisible}}, "test.unknown": { "title": "U", "detail": "U." } }""",
            ["test.unknown"]
        },
        {
            "a key that differs from a code only in case is an unknown code",
            $$"""{ "Test.Visible": { "title": "Visible", "detail": "Visible detail." }, {{OtherVisible}} }""",
            ["Test.Visible", "unknown"]
        },
        { "a duplicate message key", $$"""{ {{Visible}}, {{OtherVisible}}, {{Visible}} }""", ["test.visible"] },
        { "a file that is not valid JSON", $$"""{ {{Visible}}, {{OtherVisible}}""", ["JSON"] },
        { "a file whose root is not an object", "[]", ["object"] },
        { "a null message", $$"""{ "test.visible": null, {{OtherVisible}} }""", ["test.visible", "null"] },
        { "a message that is a string", $$"""{ "test.visible": "Visible", {{OtherVisible}} }""", ["test.visible"] },
        { "a message that is an array", $$"""{ "test.visible": [], {{OtherVisible}} }""", ["test.visible"] },
        {
            "a missing title",
            $$"""{ "test.visible": { "detail": "Visible detail." }, {{OtherVisible}} }""",
            ["test.visible", "title"]
        },
        {
            "a missing detail",
            $$"""{ "test.visible": { "title": "Visible" }, {{OtherVisible}} }""",
            ["test.visible", "detail"]
        },
        {
            "a blank title",
            $$"""{ "test.visible": { "title": " ", "detail": "Visible detail." }, {{OtherVisible}} }""",
            ["test.visible", "title"]
        },
        {
            "an empty detail",
            $$"""{ "test.visible": { "title": "Visible", "detail": "" }, {{OtherVisible}} }""",
            ["test.visible", "detail"]
        },
        {
            "a title that is not a string",
            $$"""{ "test.visible": { "title": 5, "detail": "Visible detail." }, {{OtherVisible}} }""",
            ["test.visible", "title"]
        },
        {
            "a null detail",
            $$"""{ "test.visible": { "title": "Visible", "detail": null }, {{OtherVisible}} }""",
            ["test.visible", "detail"]
        },
    };

    [Fact]
    public void Accepts_consistent_messages()
    {
        var catalog = Create($$"""{ {{Visible}}, {{OtherVisible}} }""");

        Assert.True(catalog.TryGetMessage("test.visible", out var visible));
        Assert.Equal(new ErrorMessage("Visible", "Visible detail."), visible);
        Assert.True(catalog.TryGetMessage("test.other", out var other));
        Assert.Equal(new ErrorMessage("Other", "Other detail."), other);
        Assert.False(catalog.TryGetMessage("test.server", out _));
        Assert.False(catalog.TryGetMessage("Test.Visible", out _));
    }

    [Fact]
    public void Real_messages_match_the_real_definitions()
    {
        var registry = new ApplicationErrorDefinitionRegistry(
            ApplicationErrorDefinitionSource.FromAssembly(typeof(ApplicationErrorDefinition).Assembly));

        var catalog = ErrorMessageCatalogFactory.Create(ErrorMessageSource.FromEmbeddedResource(), registry);

        Assert.True(catalog.TryGetMessage("not_found", out var notFound));
        Assert.Equal(new ErrorMessage("Not found", "No resource exists at this address."), notFound);
        Assert.True(catalog.TryGetMessage("method_not_allowed", out var methodNotAllowed));
        Assert.Equal(
            new ErrorMessage("Method not allowed", "This address does not support this request method."),
            methodNotAllowed);
        Assert.True(catalog.TryGetMessage("request.invalid", out var invalid));
        Assert.Equal(new ErrorMessage("Invalid request", "The request could not be read."), invalid);
    }

    [Theory]
    [MemberData(nameof(InconsistentMessages))]
    public void Startup_check_rejects(string inconsistency, string messages, string[] named)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Create(messages));

        Assert.All(named, text => Assert.Contains(text, exception.Message, StringComparison.Ordinal));
        Assert.NotNull(inconsistency);
    }

    private static ErrorMessageCatalog Create(string messages) =>
        ErrorMessageCatalogFactory.Create(new ErrorMessageSource(Encoding.UTF8.GetBytes(messages)), Registry);
}
