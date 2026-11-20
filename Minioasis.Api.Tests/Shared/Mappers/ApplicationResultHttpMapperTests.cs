using Microsoft.Extensions.DependencyInjection;
using Minioasis.Api.Shared.Mappers;
using Minioasis.Api.Tests.Shared.Factories;
using Minioasis.Api.Tests.Shared.TestDoubles;
using Minioasis.Application.Shared.Errors;
using Minioasis.Application.Shared.Results;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Minioasis.Api.Tests.Shared.Mappers;

public sealed class ApplicationResultHttpMapperTests
{
    public static readonly TheoryData<ApplicationErrorCategory, int> StatusTable = new()
    {
        { ApplicationErrorCategory.Validation, 400 },
        { ApplicationErrorCategory.NotFound, 404 },
        { ApplicationErrorCategory.MethodNotAllowed, 405 },
        { ApplicationErrorCategory.DependencyInvalidResponse, 502 },
        { ApplicationErrorCategory.DependencyUnavailable, 503 },
        { ApplicationErrorCategory.DependencyAuthenticationFailed, 500 },
        { ApplicationErrorCategory.Unexpected, 500 },
    };

    /// <summary>
    /// One registered definition of each category, through the mapper as an endpoint uses it. The body's code tells
    /// the mapper's answer from the framework's own 404 for a route that is missing.
    /// </summary>
    [Theory]
    [InlineData("request.invalid", HttpStatusCode.BadRequest, "request.invalid")]
    [InlineData("test.not_found?id=1", HttpStatusCode.NotFound, "test.not_found")]
    [InlineData("method_not_allowed", HttpStatusCode.MethodNotAllowed, "method_not_allowed")]
    [InlineData("koha.invalid_response", HttpStatusCode.BadGateway, "internal_error")]
    [InlineData("koha.unavailable", HttpStatusCode.ServiceUnavailable, "internal_error")]
    [InlineData("koha.authentication_failed", HttpStatusCode.InternalServerError, "internal_error")]
    [InlineData("system.unexpected", HttpStatusCode.InternalServerError, "internal_error")]
    public async Task Maps_each_category_to_its_status(string failure, HttpStatusCode expected, string code)
    {
        await using var factory = new ApiTestHostFactory(TestErrorDefinitions.Replace);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/test/failures/{failure}", TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(text);
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [MemberData(nameof(StatusTable))]
    public void Status_table_has_one_status_per_category(ApplicationErrorCategory category, int expected)
    {
        Assert.Equal(expected, ApplicationResultHttpMapper.GetStatus(category));
    }

    [Fact]
    public void Status_table_covers_exactly_the_seven_categories()
    {
        var tested = StatusTable.Select(row => row.Data.Item1).Order();

        Assert.Equal(Enum.GetValues<ApplicationErrorCategory>().Order(), tested);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8)]
    public void Undefined_category_throws(int category)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ApplicationResultHttpMapper.GetStatus((ApplicationErrorCategory)category));
    }

    [Fact]
    public async Task Success_is_answered_by_the_endpoint()
    {
        await using var factory = new ApiTestHostFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/test/success", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("done", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Null_arguments_throw()
    {
        await using var factory = new ApiTestHostFactory();
        var mapper = factory.Services.GetRequiredService<ApplicationResultHttpMapper>();
        var success = ApplicationResult<string>.Success("done");

        Assert.Equal(
            "result",
            Assert.Throws<ArgumentNullException>(() => mapper.Map<string>(null!, _ => null!)).ParamName);
        Assert.Equal("onSuccess", Assert.Throws<ArgumentNullException>(() => mapper.Map(success, null!)).ParamName);
        Assert.Equal("error", Assert.Throws<ArgumentNullException>(() => mapper.MapError(null!)).ParamName);
    }
}
