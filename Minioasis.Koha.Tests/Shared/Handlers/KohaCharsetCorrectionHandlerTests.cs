using Minioasis.Koha.Shared.Handlers;
using Minioasis.Koha.Tests.Shared.TestDoubles;
using System.Net;
using Xunit;

namespace Minioasis.Koha.Tests.Shared.Handlers;

public sealed class KohaCharsetCorrectionHandlerTests
{
    [Theory]
    [InlineData("application/json; charset=utf8")]
    [InlineData("application/json; charset=UTF8")]
    [InlineData("application/json; charset=\"utf8\"")]
    public async Task Corrects_utf8_to_utf_hyphen_8_so_that_the_body_can_be_read(string contentType)
    {
        using var response = await SendAsync(KohaResponses.Json(HttpStatusCode.Unauthorized, """{"error":"é"}""", contentType));

        Assert.Equal("utf-8", response.Content.Headers.ContentType!.CharSet);
        Assert.Equal("application/json", response.Content.Headers.ContentType.MediaType);
        Assert.Equal("""{"error":"é"}""", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("application/json; charset=UTF-8", "UTF-8")]
    [InlineData("text/html; charset=iso-8859-1", "iso-8859-1")]
    [InlineData("application/json", null)]
    public async Task Leaves_any_other_content_type_unchanged(string contentType, string? charset)
    {
        using var response = await SendAsync(KohaResponses.Json(HttpStatusCode.OK, "{}", contentType));

        Assert.Equal(charset, response.Content.Headers.ContentType!.CharSet);
    }

    [Fact]
    public async Task Leaves_a_response_without_a_content_type_unchanged()
    {
        using var response = await SendAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

        Assert.Null(response.Content.Headers.ContentType);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpResponseMessage answer)
    {
        using var invoker = new HttpMessageInvoker(
            new KohaCharsetCorrectionHandler { InnerHandler = new StubHttpMessageHandler(_ => answer) });
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://koha.test/api/v1/items/1");
        return await invoker.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
