using Minioasis.Api.Shared.Factories;
using Minioasis.Api.Shared.Models;
using Minioasis.Api.Shared.Providers;
using Minioasis.Application.Shared.Errors;
using System.Text;
using Xunit;

namespace Minioasis.Api.Tests.Shared.Providers;

public sealed class ErrorMessageProviderTests
{
    private static readonly ApplicationErrorDefinition NoPlaceholders =
        new("test.none", ApplicationErrorCategory.Validation);

    private static readonly ApplicationErrorDefinition OnePlaceholder =
        new("test.one", ApplicationErrorCategory.NotFound);

    private static readonly ApplicationErrorDefinition TwoPlaceholders =
        new("test.two", ApplicationErrorCategory.MethodNotAllowed);

    private static readonly ApplicationErrorDefinition Masked =
        new("test.masked", ApplicationErrorCategory.DependencyUnavailable);

    private const string Messages = """
        {
          "test.none": { "title": "Plain title", "detail": "Plain detail." },
          "test.one": { "title": "Item not found", "detail": "No item has identifier {id}." },
          "test.two": { "title": "Bad {field}", "detail": "The value {value} is not valid for {field}." }
        }
        """;

    public static readonly TheoryData<string, Dictionary<string, string>, string, string> FilledMessages = new()
    {
        { "test.none", new Dictionary<string, string>(), "Plain title", "Plain detail." },
        {
            "test.one", new Dictionary<string, string> { ["id"] = "77" },
            "Item not found", "No item has identifier 77."
        },
        {
            "test.two", new Dictionary<string, string> { ["value"] = "abc", ["field"] = "due date" },
            "Bad due date", "The value abc is not valid for due date."
        },
    };

    public static readonly TheoryData<string, Dictionary<string, string>, string> MissingValues = new()
    {
        { "test.one", new Dictionary<string, string>(), "id" },
        { "test.one", new Dictionary<string, string> { ["ID"] = "77" }, "id" },
        { "test.two", new Dictionary<string, string> { ["value"] = "abc" }, "field" },
        { "test.two", new Dictionary<string, string> { ["field"] = "due date" }, "value" },
    };

    public static readonly TheoryData<string, Dictionary<string, string>, string> ExtraValues = new()
    {
        { "test.none", new Dictionary<string, string> { ["id"] = "77" }, "id" },
        { "test.one", new Dictionary<string, string> { ["id"] = "77", ["name"] = "x" }, "name" },
        {
            "test.two", new Dictionary<string, string> { ["value"] = "abc", ["field"] = "f", ["Field"] = "g" },
            "Field"
        },
    };

    [Theory]
    [MemberData(nameof(FilledMessages))]
    public void Fills_zero_one_and_two_placeholders(
        string code,
        Dictionary<string, string> values,
        string title,
        string detail)
    {
        var message = CreateProvider().GetMessage(new ApplicationError(Definition(code), values));

        Assert.Equal(new ErrorMessage(title, detail), message);
    }

    [Fact]
    public void Dependency_status_is_not_a_placeholder_value()
    {
        var provider = CreateProvider();

        var plain = provider.GetMessage(new ApplicationError(NoPlaceholders, dependencyStatus: 503));
        var filled = provider.GetMessage(
            new ApplicationError(
                OnePlaceholder,
                new Dictionary<string, string> { ["id"] = "7" },
                dependencyStatus: 404));

        Assert.Equal(new ErrorMessage("Plain title", "Plain detail."), plain);
        Assert.Equal(new ErrorMessage("Item not found", "No item has identifier 7."), filled);
    }

    [Theory]
    [MemberData(nameof(MissingValues))]
    public void Missing_value_throws(string code, Dictionary<string, string> values, string placeholder)
    {
        var provider = CreateProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => provider.GetMessage(new ApplicationError(Definition(code), values)));

        Assert.Contains(code, exception.Message, StringComparison.Ordinal);
        Assert.Contains($"'{placeholder}'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ExtraValues))]
    public void Extra_value_throws(string code, Dictionary<string, string> values, string name)
    {
        var provider = CreateProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => provider.GetMessage(new ApplicationError(Definition(code), values)));

        Assert.Contains(code, exception.Message, StringComparison.Ordinal);
        Assert.Contains($"'{name}'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Values_are_inserted_as_they_are()
    {
        var values = new Dictionary<string, string> { ["value"] = "{field}", ["field"] = "{value}" };

        var message = CreateProvider().GetMessage(new ApplicationError(TwoPlaceholders, values));

        Assert.Equal(new ErrorMessage("Bad {value}", "The value {field} is not valid for {value}."), message);
    }

    [Fact]
    public void Error_without_a_message_throws()
    {
        var provider = CreateProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => provider.GetMessage(new ApplicationError(Masked)));

        Assert.Contains("test.masked", exception.Message, StringComparison.Ordinal);
    }

    private static ApplicationErrorDefinition Definition(string code) =>
        new[] { NoPlaceholders, OnePlaceholder, TwoPlaceholders }.Single(definition => definition.Code == code);

    private static ErrorMessageProvider CreateProvider() =>
        new(ErrorMessageCatalogFactory.Create(
            new ErrorMessageSource(Encoding.UTF8.GetBytes(Messages)),
            new ApplicationErrorDefinitionRegistry(
                new ApplicationErrorDefinitionSource([NoPlaceholders, OnePlaceholder, TwoPlaceholders, Masked]))));
}
