using Minioasis.Application.Shared.Errors;
using System.Reflection;
using Xunit;

namespace Minioasis.Application.Tests.Shared.Errors;

public sealed class ApplicationErrorRegistryTests
{
    [Fact]
    public void The_default_registry_holds_every_declared_definition_exactly_once()
    {
        var declared = typeof(ApplicationErrorRegistry).Assembly
            .GetTypes()
            .Where(type => type.Name.EndsWith("ErrorDefinitions", StringComparison.Ordinal))
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.FieldType == typeof(ApplicationErrorDefinition))
            .Select(field => (ApplicationErrorDefinition)field.GetValue(null)!)
            .ToList();

        Assert.NotEmpty(declared);
        Assert.Equal(
            declared.Select(definition => definition.Code).Order(StringComparer.Ordinal),
            ApplicationErrorRegistry.Default.Definitions.Select(definition => definition!.Code)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_default_registry_holds_the_codes_and_categories_of_brief_001()
    {
        var expected = new Dictionary<string, ApplicationErrorCategory>
        {
            ["items.invalid_identifier"] = ApplicationErrorCategory.Validation,
            ["items.not_found"] = ApplicationErrorCategory.NotFound,
            ["not_found"] = ApplicationErrorCategory.NotFound,
            ["method_not_allowed"] = ApplicationErrorCategory.MethodNotAllowed,
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
    [InlineData(ApplicationErrorCategory.MethodNotAllowed, true)]
    [InlineData(ApplicationErrorCategory.DependencyInvalidResponse, false)]
    [InlineData(ApplicationErrorCategory.DependencyUnavailable, false)]
    [InlineData(ApplicationErrorCategory.DependencyAuthenticationFailed, false)]
    [InlineData(ApplicationErrorCategory.Unexpected, false)]
    public void Only_errors_the_client_can_act_on_are_client_visible(ApplicationErrorCategory category, bool visible)
    {
        Assert.Equal(visible, new ApplicationErrorDefinition("x", category).IsClientVisible);
    }
}
