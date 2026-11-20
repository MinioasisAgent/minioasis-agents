using Minioasis.Application.Shared.Errors;
using Minioasis.Application.Shared.Outcomes;
using Xunit;

namespace Minioasis.Application.Tests.Shared.Errors;

public sealed class DependencyErrorDefinitionsTests
{
    [Theory]
    [InlineData(
        DependencyFailureOutcome.AuthenticationFailed,
        "koha.authentication_failed",
        ApplicationErrorCategory.DependencyAuthenticationFailed)]
    [InlineData(
        DependencyFailureOutcome.Unavailable,
        "koha.unavailable",
        ApplicationErrorCategory.DependencyUnavailable)]
    [InlineData(
        DependencyFailureOutcome.InvalidResponse,
        "koha.invalid_response",
        ApplicationErrorCategory.DependencyInvalidResponse)]
    public void Selects_definition_and_category(
        DependencyFailureOutcome outcome,
        string expectedCode,
        ApplicationErrorCategory expectedCategory)
    {
        var definition = DependencyErrorDefinitions.ForKohaOutcome(outcome);

        Assert.Equal(expectedCode, definition.Code);
        Assert.Equal(expectedCategory, definition.Category);
    }

    [Fact]
    public void Selection_returns_the_declared_definitions()
    {
        Assert.Same(
            DependencyErrorDefinitions.KohaAuthenticationFailed,
            DependencyErrorDefinitions.ForKohaOutcome(DependencyFailureOutcome.AuthenticationFailed));
        Assert.Same(
            DependencyErrorDefinitions.KohaUnavailable,
            DependencyErrorDefinitions.ForKohaOutcome(DependencyFailureOutcome.Unavailable));
        Assert.Same(
            DependencyErrorDefinitions.KohaInvalidResponse,
            DependencyErrorDefinitions.ForKohaOutcome(DependencyFailureOutcome.InvalidResponse));
    }

    [Fact]
    public void Every_outcome_has_a_definition()
    {
        foreach (var outcome in Enum.GetValues<DependencyFailureOutcome>())
        {
            Assert.NotNull(DependencyErrorDefinitions.ForKohaOutcome(outcome));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void Undefined_outcome_throws(int outcome)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DependencyErrorDefinitions.ForKohaOutcome((DependencyFailureOutcome)outcome));
    }
}
