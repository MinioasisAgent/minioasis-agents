using Minioasis.Application.Shared.Outcomes;
using Minioasis.Koha.Authentication.Exceptions;
using Minioasis.Koha.Generated.OAuth;
using Minioasis.Koha.Generated.OAuth.Contracts;
using Minioasis.Koha.Shared.Exceptions;
using Minioasis.Koha.Shared.Mappers;
using Minioasis.Koha.Tests.Shared.TestDoubles;
using System.Net;
using Xunit;

namespace Minioasis.Koha.Tests.Shared.Mappers;

/// <summary>
/// Spec 001, R20 and D8: the shared translation of Koha failures into <see cref="DependencyFailureOutcome"/>, as one
/// table (<c>ER-010</c>). At the call step an adapter passes the status it read from Koha's reply or from the
/// generated client's exception; at either step it passes the documented exception it caught.
/// </summary>
public sealed class KohaProtocolFailureMapperTests
{
    // R20, call step: 401 and 403 are an authentication failure; 429 and 500 to 599 are "unavailable"; every other
    // status a capability does not claim is an unusable response: 100 to 199, any 2xx that reaches the mapper (an
    // undefined 2xx, or a body that does not match), 300 to 399, the other 4xx, and 600 to 999.
    [Theory]
    [InlineData(100, DependencyFailureOutcome.InvalidResponse)]
    [InlineData(199, DependencyFailureOutcome.InvalidResponse)]
    [InlineData(200, DependencyFailureOutcome.InvalidResponse)]
    [InlineData(201, DependencyFailureOutcome.InvalidResponse)]
    [InlineData(204, DependencyFailureOutcome.InvalidResponse)]
    [InlineData(299, DependencyFailureOutcome.InvalidResponse)]
    [InlineData(300, DependencyFailureOutcome.InvalidResponse)]
    [InlineData(302, DependencyFailureOutcome.InvalidResponse)]
    [InlineData(399, DependencyFailureOutcome.InvalidResponse)]
    [InlineData(400, DependencyFailureOutcome.InvalidResponse)]
    [InlineData(401, DependencyFailureOutcome.AuthenticationFailed)]
    [InlineData(402, DependencyFailureOutcome.InvalidResponse)]
    [InlineData(403, DependencyFailureOutcome.AuthenticationFailed)]
    [InlineData(404, DependencyFailureOutcome.InvalidResponse)]
    [InlineData(428, DependencyFailureOutcome.InvalidResponse)]
    [InlineData(429, DependencyFailureOutcome.Unavailable)]
    [InlineData(430, DependencyFailureOutcome.InvalidResponse)]
    [InlineData(499, DependencyFailureOutcome.InvalidResponse)]
    [InlineData(500, DependencyFailureOutcome.Unavailable)]
    [InlineData(502, DependencyFailureOutcome.Unavailable)]
    [InlineData(503, DependencyFailureOutcome.Unavailable)]
    [InlineData(599, DependencyFailureOutcome.Unavailable)]
    [InlineData(600, DependencyFailureOutcome.InvalidResponse)]
    [InlineData(999, DependencyFailureOutcome.InvalidResponse)]
    public void Maps_statuses(int status, DependencyFailureOutcome expected)
    {
        Assert.Equal(expected, KohaProtocolFailureMapper.Map(status));
    }

    // R20: the documented exceptions of a send through the Koha clients (KohaClientNames). The token step's
    // refusal is an authentication failure; Koha being unreachable at either step (a status, a network error, or
    // the deadline) is "unavailable"; a network error at the call step, an HttpRequestException (a body that broke
    // off included, around its IOException), is "unavailable".
    [Theory]
    [MemberData(nameof(DocumentedExceptions))]
    public void Maps_exceptions(Exception exception, DependencyFailureOutcome expected)
    {
        Assert.Equal(expected, KohaProtocolFailureMapper.Map(exception));
    }

    public static TheoryData<Exception, DependencyFailureOutcome> DocumentedExceptions => new()
    {
        { new KohaAuthenticationException(400), DependencyFailureOutcome.AuthenticationFailed },
        { new KohaAuthenticationException(200), DependencyFailureOutcome.AuthenticationFailed },
        { new KohaUnavailableException(503), DependencyFailureOutcome.Unavailable },
        { new KohaUnavailableException(), DependencyFailureOutcome.Unavailable },
        { new KohaUnavailableException(new HttpRequestException("test")), DependencyFailureOutcome.Unavailable },
        { new HttpRequestException("test"), DependencyFailureOutcome.Unavailable },
        { new HttpRequestException("test", new IOException("test")), DependencyFailureOutcome.Unavailable },
    };

    // ER-009, CD-005: a cancellation, or any exception that the Koha clients do not document, is not a Koha
    // failure; the mapper refuses it rather than turn it into an outcome. Its message never repeats the exception's.
    // A bare IOException is one of them: the clients read the body, so a broken body arrives wrapped (B1).
    [Theory]
    [MemberData(nameof(UndocumentedExceptions))]
    public void Refuses_an_undocumented_exception(Exception exception)
    {
        var refused = Assert.Throws<ArgumentException>(() => KohaProtocolFailureMapper.Map(exception));

        Assert.Equal("exception", refused.ParamName);
        Assert.DoesNotContain("marker", refused.Message, StringComparison.Ordinal);
        Assert.Null(refused.InnerException);
    }

    public static TheoryData<Exception> UndocumentedExceptions => new()
    {
        new OperationCanceledException("marker"),
        new TaskCanceledException("marker"),
        new InvalidOperationException("marker"),
        new KohaConfigurationException("marker"),
        new IOException("marker"),
    };

    [Fact]
    public void Null_exception_throws()
    {
        Assert.Equal(
            "exception",
            Assert.Throws<ArgumentNullException>(() => KohaProtocolFailureMapper.Map(null!)).ParamName);
    }

    // R20, last row, through a real generated client: the generated exception's status is what the adapter passes.
    // A 2xx reply that the contract defines but whose complete body cannot be parsed or has a field of the wrong
    // type, and a 2xx the contract does not define, become an unusable response; other statuses follow the table.
    // The OAuth client stands in for the generated clients of later capabilities, which follow the same template.
    [Theory]
    [InlineData(200, """{"access_token":"t","token_type":"Bearer","expires_in":"not a number"}""", DependencyFailureOutcome.InvalidResponse)]
    [InlineData(200, """{"access_token":""", DependencyFailureOutcome.InvalidResponse)]
    [InlineData(200, "", DependencyFailureOutcome.InvalidResponse)]
    [InlineData(201, """{"access_token":"t","token_type":"Bearer","expires_in":3600}""", DependencyFailureOutcome.InvalidResponse)]
    [InlineData(302, "", DependencyFailureOutcome.InvalidResponse)]
    [InlineData(400, """{"error":"bad"}""", DependencyFailureOutcome.InvalidResponse)]
    [InlineData(401, """{"error":"no"}""", DependencyFailureOutcome.AuthenticationFailed)]
    [InlineData(403, """{"error":"no"}""", DependencyFailureOutcome.AuthenticationFailed)]
    [InlineData(503, "down", DependencyFailureOutcome.Unavailable)]
    public async Task Maps_generated_exception_status(int status, string body, DependencyFailureOutcome expected)
    {
        var stub = new StubHttpMessageHandler(() => StubHttpMessageHandler.Reply((HttpStatusCode)status, body));
        using var httpClient = new HttpClient(stub);
        var client = new GeneratedKohaOAuthClient(httpClient) { BaseUrl = "https://koha.test/api/v1/" };

        var exception = await Assert.ThrowsAnyAsync<KohaServiceException>(() => client
            .TokenOAuthAsync(
                new Body { Grant_type = "client_credentials", Client_id = "id", Client_secret = "s" },
                TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        Assert.Equal(status, exception.StatusCode);
        Assert.Equal(expected, KohaProtocolFailureMapper.Map(exception.StatusCode));
    }
}
