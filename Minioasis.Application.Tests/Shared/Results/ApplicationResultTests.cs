using Minioasis.Application.Shared.Errors;
using Minioasis.Application.Shared.Results;
using Xunit;

namespace Minioasis.Application.Tests.Shared.Results;

public sealed class ApplicationResultTests
{
    [Fact]
    public void Success_carries_the_response_and_no_error()
    {
        var response = new TestResponse("value");

        var result = ApplicationResult<TestResponse>.Success(response);

        Assert.True(result.IsSuccess);
        Assert.Same(response, result.Response);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_carries_the_error_and_no_response()
    {
        var error = new ApplicationError(new ApplicationErrorDefinition("test.code", ApplicationErrorCategory.NotFound));

        var result = ApplicationResult<TestResponse>.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Null(result.Response);
    }

    [Fact]
    public void Success_with_null_response_throws()
    {
        Assert.Throws<ArgumentNullException>(() => ApplicationResult<TestResponse>.Success(null!));
    }

    [Fact]
    public void Failure_with_null_error_throws()
    {
        Assert.Throws<ArgumentNullException>(() => ApplicationResult<TestResponse>.Failure(null!));
    }

    private sealed record TestResponse(string Value);
}
