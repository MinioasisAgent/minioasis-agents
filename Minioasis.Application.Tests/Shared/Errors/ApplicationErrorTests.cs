using Minioasis.Application.Shared.Errors;
using Xunit;

namespace Minioasis.Application.Tests.Shared.Errors;

public sealed class ApplicationErrorTests
{
    private static readonly ApplicationErrorDefinition Definition =
        new("test.code", ApplicationErrorCategory.NotFound);

    [Fact]
    public void Carries_definition_placeholder_values_and_dependency_status()
    {
        var error = new ApplicationError(
            Definition,
            new Dictionary<string, string> { ["id"] = "77", ["name"] = "Loan" },
            dependencyStatus: 503);

        Assert.Same(Definition, error.Definition);
        Assert.Equal("77", error.PlaceholderValues["id"]);
        Assert.Equal("Loan", error.PlaceholderValues["name"]);
        Assert.Equal(2, error.PlaceholderValues.Count);
        Assert.Equal(503, error.DependencyStatus);
    }

    [Fact]
    public void Dependency_status_is_not_a_placeholder_value()
    {
        var error = new ApplicationError(Definition, placeholderValues: null, dependencyStatus: 401);

        Assert.Empty(error.PlaceholderValues);
        Assert.Equal(401, error.DependencyStatus);
    }

    [Fact]
    public void Defaults_to_no_placeholder_values_and_no_dependency_status()
    {
        var error = new ApplicationError(Definition);

        Assert.Empty(error.PlaceholderValues);
        Assert.Null(error.DependencyStatus);
    }

    [Fact]
    public void Placeholder_values_are_copied()
    {
        var values = new Dictionary<string, string> { ["id"] = "77" };

        var error = new ApplicationError(Definition, values);
        values["id"] = "78";
        values["extra"] = "value";

        Assert.Equal("77", error.PlaceholderValues["id"]);
        Assert.Single(error.PlaceholderValues);
    }

    [Fact]
    public void Null_definition_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ApplicationError(null!));
    }

    [Fact]
    public void Null_placeholder_value_throws()
    {
        var values = new Dictionary<string, string> { ["id"] = null! };

        var exception = Assert.Throws<ArgumentException>(() => new ApplicationError(Definition, values));

        Assert.Contains("id", exception.Message, StringComparison.Ordinal);
    }
}
