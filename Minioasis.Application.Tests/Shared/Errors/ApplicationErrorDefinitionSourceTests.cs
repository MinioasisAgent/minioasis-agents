using Minioasis.Application.Shared.Errors;
using Xunit;

namespace Minioasis.Application.Tests.Shared.Errors;

public sealed class ApplicationErrorDefinitionSourceTests
{
    [Fact]
    public void Scan_collects_every_public_static_definition_field_of_marked_classes_including_null_ones()
    {
        var source = ApplicationErrorDefinitionSource.FromAssembly(typeof(ApplicationErrorDefinitionSourceTests).Assembly);

        Assert.Contains(MarkedTestErrorDefinitions.First, source.Definitions);
        Assert.Contains(MarkedTestErrorDefinitions.Second, source.Definitions);
        Assert.Contains(null, source.Definitions);
    }

    [Fact]
    public void Scan_ignores_unmarked_classes()
    {
        var source = ApplicationErrorDefinitionSource.FromAssembly(typeof(ApplicationErrorDefinitionSourceTests).Assembly);

        Assert.DoesNotContain(UnmarkedTestErrorDefinitions.Ignored, source.Definitions);
    }

    [Fact]
    public void Scan_of_the_application_assembly_finds_the_seven_definitions()
    {
        var source = ApplicationErrorDefinitionSource.FromAssembly(typeof(ApplicationErrorDefinition).Assembly);

        Assert.Equal(
            [
                "koha.authentication_failed",
                "koha.invalid_response",
                "koha.unavailable",
                "method_not_allowed",
                "not_found",
                "request.invalid",
                "system.unexpected",
            ],
            source.Definitions.Select(definition => definition!.Code).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Explicit_definitions_are_kept_in_order()
    {
        var first = new ApplicationErrorDefinition("test.first", ApplicationErrorCategory.Validation);
        var second = new ApplicationErrorDefinition("test.second", ApplicationErrorCategory.NotFound);

        var source = new ApplicationErrorDefinitionSource([second, null, first]);

        Assert.Equal([second, null, first], source.Definitions);
    }

    [ErrorDefinitions]
    private static class MarkedTestErrorDefinitions
    {
        public static readonly ApplicationErrorDefinition First =
            new("test.scanned.first", ApplicationErrorCategory.Validation);

        public static readonly ApplicationErrorDefinition Second =
            new("test.scanned.second", ApplicationErrorCategory.DependencyUnavailable);

        public static readonly ApplicationErrorDefinition? Missing = null;

        public static readonly string NotADefinition = "test.not_a_definition";
    }

    private static class UnmarkedTestErrorDefinitions
    {
        public static readonly ApplicationErrorDefinition Ignored =
            new("test.unmarked", ApplicationErrorCategory.NotFound);
    }
}
