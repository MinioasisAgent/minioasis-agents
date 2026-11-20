using Minioasis.Api.Tests.Shared.Factories;
using Minioasis.Api.Tests.Shared.TestDoubles;
using System.Net;
using Xunit;

namespace Minioasis.Api.Tests.Shared.Writers;

public sealed class ErrorResponseTests
{
    public static readonly TheoryData<string, HttpStatusCode, string, string, string> ClientVisibleErrors = new()
    {
        // A framework error, and the same message through the mapper: no placeholders.
        { "/does-not-exist", HttpStatusCode.NotFound, "not_found", "Not found", "No resource exists at this address." },
        {
            "/test/failures/method_not_allowed", HttpStatusCode.MethodNotAllowed, "method_not_allowed",
            "Method not allowed", "This address does not support this request method."
        },
        // A capability's error with one placeholder.
        {
            "/test/failures/test.not_found?id=77", HttpStatusCode.NotFound, "test.not_found",
            "Test item not found", "No test item exists with identifier 77."
        },
        // Two placeholders; one of them in both the title and the detail.
        {
            "/test/failures/test.two_placeholders?value=abc&field=due%20date", HttpStatusCode.BadRequest,
            "test.two_placeholders", "Invalid test value for due date", "The value abc is not valid for due date."
        },
    };

    public static readonly TheoryData<string, HttpStatusCode, string> ServerFailures = new()
    {
        { "/test/failures/koha.authentication_failed", HttpStatusCode.InternalServerError, "Development" },
        { "/test/failures/koha.authentication_failed", HttpStatusCode.InternalServerError, "Production" },
        {
            "/test/failures/koha.authentication_failed?dependencyStatus=401", HttpStatusCode.InternalServerError,
            "Production"
        },
        { "/test/failures/koha.invalid_response", HttpStatusCode.BadGateway, "Development" },
        { "/test/failures/koha.invalid_response", HttpStatusCode.BadGateway, "Production" },
        { "/test/failures/koha.unavailable", HttpStatusCode.ServiceUnavailable, "Development" },
        { "/test/failures/koha.unavailable", HttpStatusCode.ServiceUnavailable, "Production" },
        { "/test/failures/koha.unavailable?dependencyStatus=503", HttpStatusCode.ServiceUnavailable, "Development" },
        { "/test/failures/system.unexpected", HttpStatusCode.InternalServerError, "Development" },
        { "/test/failures/system.unexpected", HttpStatusCode.InternalServerError, "Production" },
        { "/test/throw", HttpStatusCode.InternalServerError, "Development" },
        { "/test/throw", HttpStatusCode.InternalServerError, "Production" },
        { "/test/ambiguous", HttpStatusCode.InternalServerError, "Development" },
        { "/test/ambiguous", HttpStatusCode.InternalServerError, "Production" },
    };

    [Theory]
    [MemberData(nameof(ClientVisibleErrors))]
    public async Task Client_visible_error_has_exact_members_and_content_type(
        string path,
        HttpStatusCode status,
        string code,
        string title,
        string detail)
    {
        await using var factory = new ApiTestHostFactory(TestErrorDefinitions.Replace);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        await ProblemResponseAssert.ClientVisibleAsync(response, status, code, title, detail);
    }

    [Theory]
    [InlineData("/test/failures/test.not_found")]
    [InlineData("/test/failures/test.two_placeholders?value=abc")]
    [InlineData("/test/failures/test.not_found?ID=77")]
    [InlineData("/test/failures/test.not_found?id=77&extra=1")]
    [InlineData("/test/failures/not_found?id=77")]
    public async Task Placeholder_mismatch_is_answered_as_masked_500(string path)
    {
        await using var factory = new ApiTestHostFactory(TestErrorDefinitions.Replace);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        await ProblemResponseAssert.MaskedAsync(response, HttpStatusCode.InternalServerError);
    }

    [Theory]
    [MemberData(nameof(ServerFailures))]
    public async Task Server_failure_is_masked(string path, HttpStatusCode status, string environment)
    {
        await using var factory = new ApiTestHostFactory(environment: environment);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        await ProblemResponseAssert.MaskedAsync(response, status);
    }
}
