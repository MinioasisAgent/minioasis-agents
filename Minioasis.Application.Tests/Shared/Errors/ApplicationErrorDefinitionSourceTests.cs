using Minioasis.Application.Shared.Errors;
using System.Reflection;
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

    [Fact]
    public void Every_definition_in_the_application_assembly_is_a_public_static_field_of_a_marked_class()
    {
        const BindingFlags anyStatic =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var members = typeof(ApplicationErrorDefinition).Assembly.GetTypes()
            .SelectMany(type => type.GetMembers(anyStatic))
            .Where(member => member switch
            {
                FieldInfo field => field.FieldType == typeof(ApplicationErrorDefinition),
                PropertyInfo property => property.PropertyType == typeof(ApplicationErrorDefinition),
                _ => false,
            })
            .ToList();

        var misplaced = members
            .Where(member => member is not FieldInfo { IsPublic: true }
                || member.DeclaringType is not { IsClass: true } declaringType
                || !declaringType.IsDefined(typeof(ErrorDefinitionsAttribute), inherit: false))
            .Select(member => $"{member.DeclaringType?.FullName}.{member.Name}");

        Assert.NotEmpty(members);
        Assert.Empty(misplaced);
    }

    [Fact]
    public void Null_definitions_throw()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new ApplicationErrorDefinitionSource(null!));

        Assert.Equal("definitions", exception.ParamName);
    }

    [Fact]
    public void Null_assembly_throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => ApplicationErrorDefinitionSource.FromAssembly(null!));

        Assert.Equal("assembly", exception.ParamName);
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
