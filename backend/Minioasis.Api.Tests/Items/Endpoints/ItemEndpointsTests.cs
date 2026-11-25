using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Minioasis.Api.Tests.Items.TestDoubles;
using Minioasis.Api.Tests.Shared;
using Minioasis.Api.Tests.Shared.TestDoubles;
using Minioasis.Application.Items.Abstractions;
using Minioasis.Application.Items.Outcomes;
using Minioasis.Application.Items.Responses;
using Minioasis.Application.Items.Results;
using Minioasis.Application.Items.UseCases;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Minioasis.Api.Tests.Items.Endpoints;

public sealed class ItemEndpointsTests
{
    [Fact]
    public async Task Returns_200_with_the_four_fields_as_strings_and_the_route_value_reaches_the_gateway()
    {
        var gateway = new FakeGetItemGateway(GetItemGatewayResult.Found(new GetItemResponse("12", "7", "BC-1", "QA 76")));
        await using var factory = HostWith(gateway);

        using var response = await factory.CreateClient().GetAsync("/api/v1/items/12", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await ReadJsonAsync(response);
        Assert.Equal(
            ["barcode", "bibliographicRecordId", "callNumber", "id"],
            body.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("12", body.GetProperty("id").GetString());
        Assert.Equal("7", body.GetProperty("bibliographicRecordId").GetString());
        Assert.Equal("BC-1", body.GetProperty("barcode").GetString());
        Assert.Equal("QA 76", body.GetProperty("callNumber").GetString());
        Assert.Equal(12, gateway.LastItemId!.Value);
    }

    [Fact]
    public async Task Returns_null_for_a_missing_barcode_and_call_number()
    {
        var gateway = new FakeGetItemGateway(GetItemGatewayResult.Found(new GetItemResponse("12", "7", null, null)));
        await using var factory = HostWith(gateway);

        using var response = await factory.CreateClient().GetAsync("/api/v1/items/12", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("barcode").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("callNumber").ValueKind);
    }

    [Theory]
    [InlineData("%20")]
    [InlineData("%2012")]
    [InlineData("12%20")]
    [InlineData("007")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("+5")]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("2147483648")]
    public async Task Returns_400_for_an_invalid_identifier_without_calling_koha(string id)
    {
        var gateway = new FakeGetItemGateway(GetItemGatewayResult.Found(new GetItemResponse("1", "1", null, null)));
        await using var factory = HostWith(gateway);

        using var response = await factory.CreateClient().GetAsync($"/api/v1/items/{id}", TestContext.Current.CancellationToken);

        await ProblemAssert.HasShapeAsync(
            response,
            HttpStatusCode.BadRequest,
            "items.invalid_identifier",
            "Invalid item identifier",
            "The item identifier must be a valid positive identifier.");
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task Returns_404_with_the_number_in_the_detail_when_koha_has_no_such_item()
    {
        var gateway = new FakeGetItemGateway(GetItemGatewayResult.Failed(GetItemOutcome.NotFound));
        await using var factory = HostWith(gateway);

        using var response = await factory.CreateClient().GetAsync("/api/v1/items/2147483647", TestContext.Current.CancellationToken);

        await ProblemAssert.HasShapeAsync(
            response,
            HttpStatusCode.NotFound,
            "items.not_found",
            "Item not found",
            "No item was found with identifier 2147483647.");
    }

    [Theory]
    [InlineData(GetItemOutcome.AuthenticationFailed, HttpStatusCode.InternalServerError)]
    [InlineData(GetItemOutcome.InvalidResponse, HttpStatusCode.BadGateway)]
    [InlineData(GetItemOutcome.Unavailable, HttpStatusCode.ServiceUnavailable)]
    public async Task Masks_each_koha_failure_with_its_status(GetItemOutcome outcome, HttpStatusCode status)
    {
        await using var factory = HostWith(new FakeGetItemGateway(GetItemGatewayResult.Failed(outcome)));

        using var response = await factory.CreateClient().GetAsync("/api/v1/items/12", TestContext.Current.CancellationToken);

        await ProblemAssert.IsMaskedAsync(response, status);
    }

    [Fact]
    public async Task The_unmodified_host_resolves_the_koha_gateway_and_the_use_case()
    {
        await using var factory = new ApiTestHostFactory();
        _ = factory.Server;
        using var scope = factory.Services.CreateScope();

        Assert.Equal("KohaGetItemAdapter", scope.ServiceProvider.GetRequiredService<IGetItemGateway>().GetType().Name);
        Assert.Equal("GetItemUseCase", scope.ServiceProvider.GetRequiredService<IGetItemUseCase>().GetType().Name);
    }

    private static ApiTestHostFactory HostWith(IGetItemGateway gateway) =>
        new(services =>
        {
            services.RemoveAll<IGetItemGateway>();
            services.AddSingleton(gateway);
        });

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement.Clone();
}
