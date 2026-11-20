using Minioasis.Application.Shared.Errors;
using Xunit;

namespace Minioasis.Application.Tests.Shared.Errors;

public sealed class ApplicationErrorDefinitionRegistryTests
{
    [Fact]
    public void Accepts_consistent_definitions()
    {
        var first = new ApplicationErrorDefinition("test.first", ApplicationErrorCategory.Validation);
        var second = new ApplicationErrorDefinition("test.second", ApplicationErrorCategory.Unexpected);

        var registry = new ApplicationErrorDefinitionRegistry(new ApplicationErrorDefinitionSource([first, second]));

        Assert.Equal([first, second], registry.Definitions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\n")]
    public void Rejects_blank_code(string? code)
    {
        var source = new ApplicationErrorDefinitionSource(
        [
            new ApplicationErrorDefinition("test.valid", ApplicationErrorCategory.NotFound),
            new ApplicationErrorDefinition(code!, ApplicationErrorCategory.NotFound),
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() => new ApplicationErrorDefinitionRegistry(source));

        Assert.Contains("blank", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_duplicate_code()
    {
        var source = new ApplicationErrorDefinitionSource(
        [
            new ApplicationErrorDefinition("test.duplicate", ApplicationErrorCategory.NotFound),
            new ApplicationErrorDefinition("test.other", ApplicationErrorCategory.NotFound),
            new ApplicationErrorDefinition("test.duplicate", ApplicationErrorCategory.Unexpected),
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() => new ApplicationErrorDefinitionRegistry(source));

        Assert.Contains("test.duplicate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_null_definition()
    {
        var source = new ApplicationErrorDefinitionSource(
        [
            new ApplicationErrorDefinition("test.valid", ApplicationErrorCategory.NotFound),
            null,
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() => new ApplicationErrorDefinitionRegistry(source));

        Assert.Contains("null", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8)]
    [InlineData(int.MaxValue)]
    public void Rejects_undefined_category(int category)
    {
        var source = new ApplicationErrorDefinitionSource(
        [
            new ApplicationErrorDefinition("test.valid", ApplicationErrorCategory.NotFound),
            new ApplicationErrorDefinition("test.undefined_category", (ApplicationErrorCategory)category),
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() => new ApplicationErrorDefinitionRegistry(source));

        Assert.Contains("test.undefined_category", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_empty_registry()
    {
        var source = new ApplicationErrorDefinitionSource([]);

        var exception = Assert.Throws<InvalidOperationException>(() => new ApplicationErrorDefinitionRegistry(source));

        Assert.Contains("empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Null_source_throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new ApplicationErrorDefinitionRegistry(null!));

        Assert.Equal("source", exception.ParamName);
    }
}
