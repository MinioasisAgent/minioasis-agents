using Minioasis.Application.Shared.Errors;
using Xunit;

namespace Minioasis.Application.Tests.Shared.Errors;

public sealed class ApplicationErrorDefinitionTests
{
    public static readonly TheoryData<ApplicationErrorCategory, bool> Visibility = new()
    {
        { ApplicationErrorCategory.Validation, true },
        { ApplicationErrorCategory.NotFound, true },
        { ApplicationErrorCategory.MethodNotAllowed, true },
        { ApplicationErrorCategory.DependencyInvalidResponse, false },
        { ApplicationErrorCategory.DependencyUnavailable, false },
        { ApplicationErrorCategory.DependencyAuthenticationFailed, false },
        { ApplicationErrorCategory.Unexpected, false },
    };

    [Theory]
    [MemberData(nameof(Visibility))]
    public void Is_client_visible_only_for_validation_not_found_and_method_not_allowed(
        ApplicationErrorCategory category,
        bool expected)
    {
        var definition = new ApplicationErrorDefinition("test.code", category);

        Assert.Equal(expected, definition.IsClientVisible);
    }

    [Fact]
    public void There_are_exactly_the_seven_categories_of_R1()
    {
        var tested = Visibility.Select(row => row.Data.Item1).Order();

        Assert.Equal(tested, Enum.GetValues<ApplicationErrorCategory>().Order());
    }

    [Fact]
    public void Carries_code_and_category()
    {
        var definition = new ApplicationErrorDefinition("test.code", ApplicationErrorCategory.DependencyUnavailable);

        Assert.Equal("test.code", definition.Code);
        Assert.Equal(ApplicationErrorCategory.DependencyUnavailable, definition.Category);
    }
}
