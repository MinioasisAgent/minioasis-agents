using Minioasis.Application.Shared.Errors;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Minioasis.Application.Tests.Shared.Errors;

public sealed class ApplicationErrorRegistryTests
{
    [Fact]
    public void Discover_collects_only_the_readonly_static_fields_of_marked_groups()
    {
        var registry = ApplicationErrorRegistry.Discover([typeof(MarkedGroup), typeof(UnmarkedGroup)]);

        Assert.Equal(
            ["marked.first", "marked.second"],
            registry.Definitions.Select(definition => definition!.Code).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_default_registry_holds_every_definition_the_application_declares_exactly_once()
    {
        const BindingFlags AnyStatic =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var declaringMembers = typeof(ApplicationErrorRegistry).Assembly
            .GetTypes()
            .Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), false))
            .SelectMany(type => type.GetFields(AnyStatic)
                .Where(field => !field.IsDefined(typeof(CompilerGeneratedAttribute), false))
                .Where(field => field.FieldType == typeof(ApplicationErrorDefinition))
                .Cast<MemberInfo>()
                .Concat(type.GetProperties(AnyStatic)
                    .Where(property => property.PropertyType == typeof(ApplicationErrorDefinition))))
            .ToList();

        Assert.Empty(declaringMembers
            .Select(member => member.DeclaringType!)
            .Where(type => !type.IsDefined(typeof(ErrorDefinitionsAttribute), false))
            .Select(type => type.FullName)
            .Distinct());
        Assert.Empty(declaringMembers
            .Where(member => member is not FieldInfo { IsPublic: true, IsStatic: true, IsInitOnly: true })
            .Select(member => $"{member.DeclaringType!.FullName}.{member.Name}"));

        var declared = declaringMembers
            .Cast<FieldInfo>()
            .Select(field => (ApplicationErrorDefinition)field.GetValue(null)!)
            .ToList();

        Assert.NotEmpty(declared);
        Assert.Equal(declared.Count, ApplicationErrorRegistry.Default.Definitions.Count);
        Assert.All(declared, definition => Assert.Single(
            ApplicationErrorRegistry.Default.Definitions,
            registered => ReferenceEquals(registered, definition)));
    }

    [Fact]
    public void The_default_registry_holds_the_codes_and_categories_of_briefs_001_and_002()
    {
        var expected = new Dictionary<string, ApplicationErrorCategory>
        {
            ["access.keys_unavailable"] = ApplicationErrorCategory.DependencyUnavailable,
            ["items.invalid_identifier"] = ApplicationErrorCategory.Validation,
            ["items.not_found"] = ApplicationErrorCategory.NotFound,
            ["koha.authentication_failed"] = ApplicationErrorCategory.DependencyAuthenticationFailed,
            ["koha.invalid_response"] = ApplicationErrorCategory.DependencyInvalidResponse,
            ["koha.unavailable"] = ApplicationErrorCategory.DependencyUnavailable,
            ["system.unexpected"] = ApplicationErrorCategory.Unexpected,
        };

        Assert.Equal(
            expected.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            ApplicationErrorRegistry.Default.Definitions
                .Select(definition => KeyValuePair.Create(definition!.Code, definition.Category))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(ApplicationErrorCategory.Validation, true)]
    [InlineData(ApplicationErrorCategory.NotFound, true)]
    [InlineData(ApplicationErrorCategory.DependencyInvalidResponse, false)]
    [InlineData(ApplicationErrorCategory.DependencyUnavailable, false)]
    [InlineData(ApplicationErrorCategory.DependencyAuthenticationFailed, false)]
    [InlineData(ApplicationErrorCategory.Unexpected, false)]
    public void Only_errors_the_client_can_act_on_are_client_visible(ApplicationErrorCategory category, bool visible)
    {
        Assert.Equal(visible, new ApplicationErrorDefinition("x", category).IsClientVisible);
    }

    [Fact]
    public void The_categories_are_those_of_er_004_and_none_belongs_to_http_itself()
    {
        Assert.Equal(
            [
                nameof(ApplicationErrorCategory.Validation),
                nameof(ApplicationErrorCategory.NotFound),
                nameof(ApplicationErrorCategory.DependencyInvalidResponse),
                nameof(ApplicationErrorCategory.DependencyUnavailable),
                nameof(ApplicationErrorCategory.DependencyAuthenticationFailed),
                nameof(ApplicationErrorCategory.Unexpected),
            ],
            Enum.GetNames<ApplicationErrorCategory>());
    }

    [ErrorDefinitions]
    private static class MarkedGroup
    {
        public static readonly ApplicationErrorDefinition First =
            new("marked.first", ApplicationErrorCategory.Validation);

        public static readonly ApplicationErrorDefinition Second =
            new("marked.second", ApplicationErrorCategory.Unexpected);

        public static ApplicationErrorDefinition NotReadonly =
            new("marked.not_readonly", ApplicationErrorCategory.Validation);

        public static ApplicationErrorDefinition Property
        {
            get;
        } = new("marked.property", ApplicationErrorCategory.Validation);
    }

    private static class UnmarkedGroup
    {
        public static readonly ApplicationErrorDefinition Unmarked =
            new("unmarked.field", ApplicationErrorCategory.Validation);
    }
}
