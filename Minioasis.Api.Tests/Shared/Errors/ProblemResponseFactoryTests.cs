using Minioasis.Api.Shared.Errors;
using Minioasis.Application.Shared.Errors;
using Xunit;

namespace Minioasis.Api.Tests.Shared.Errors;

public sealed class ProblemResponseFactoryTests
{
    [Theory]
    [InlineData(ApplicationErrorCategory.Validation, 400)]
    [InlineData(ApplicationErrorCategory.NotFound, 404)]
    [InlineData(ApplicationErrorCategory.MethodNotAllowed, 405)]
    [InlineData(ApplicationErrorCategory.DependencyInvalidResponse, 502)]
    [InlineData(ApplicationErrorCategory.DependencyUnavailable, 503)]
    [InlineData(ApplicationErrorCategory.DependencyAuthenticationFailed, 500)]
    [InlineData(ApplicationErrorCategory.Unexpected, 500)]
    public void Each_category_has_its_fixed_status(ApplicationErrorCategory category, int status)
    {
        Assert.Equal(status, ProblemResponseFactory.StatusFor(category));
    }

    [Fact]
    public void An_unknown_category_has_no_status()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ProblemResponseFactory.StatusFor((ApplicationErrorCategory)99));
    }
}
