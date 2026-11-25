using Minioasis.Application.Items.Errors;
using Minioasis.Application.Items.Outcomes;
using Minioasis.Application.Items.Requests;
using Minioasis.Application.Items.Responses;
using Minioasis.Application.Items.Results;
using Minioasis.Application.Items.UseCases;
using Minioasis.Application.Shared.Errors;
using Minioasis.Application.Tests.Items.TestDoubles;
using Xunit;

namespace Minioasis.Application.Tests.Items.UseCases;

public sealed class GetItemUseCaseTests
{
    private static readonly GetItemResponse Item = new("12", "7", "BC-1", "QA 76");

    [Fact]
    public async Task Returns_the_item_that_the_gateway_found()
    {
        var gateway = new FakeGetItemGateway(GetItemGatewayResult.Found(Item));

        var result = await new GetItemUseCase(gateway).ExecuteAsync(
            new GetItemRequest("12"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Same(Item, result.Value);
        Assert.Equal(12, gateway.LastItemId!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" 12")]
    [InlineData("007")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("+5")]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("2147483648")]
    public async Task Rejects_an_invalid_identifier_without_calling_the_gateway(string? id)
    {
        var gateway = new FakeGetItemGateway(GetItemGatewayResult.Found(Item));

        var result = await new GetItemUseCase(gateway).ExecuteAsync(
            new GetItemRequest(id),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Same(ItemErrorDefinitions.InvalidIdentifier, result.Error.Definition);
        Assert.Equal(GetItemUseCase.Operation, result.Error.Operation);
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task Reports_a_missing_item_as_not_found_with_its_number()
    {
        var gateway = new FakeGetItemGateway(GetItemGatewayResult.Failed(GetItemOutcome.NotFound));

        var result = await new GetItemUseCase(gateway).ExecuteAsync(
            new GetItemRequest("2147483647"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Same(ItemErrorDefinitions.NotFound, result.Error.Definition);
        Assert.Equal("2147483647", result.Error.Metadata["id"]);
        Assert.Equal("items.get", result.Error.Operation);
    }

    public static TheoryData<GetItemOutcome, string> DependencyFailures => new()
    {
        { GetItemOutcome.AuthenticationFailed, "koha.authentication_failed" },
        { GetItemOutcome.InvalidResponse, "koha.invalid_response" },
        { GetItemOutcome.Unavailable, "koha.unavailable" },
    };

    [Theory]
    [MemberData(nameof(DependencyFailures))]
    public async Task Reports_each_dependency_failure_with_its_shared_definition(GetItemOutcome outcome, string code)
    {
        var gateway = new FakeGetItemGateway(GetItemGatewayResult.Failed(outcome));

        var result = await new GetItemUseCase(gateway).ExecuteAsync(
            new GetItemRequest("12"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.Error.Definition.Code);
        Assert.False(result.Error.Definition.IsClientVisible);
        Assert.Equal("items.get", result.Error.Operation);
    }

    [Fact]
    public async Task Passes_the_cancellation_token_to_the_gateway()
    {
        using var source = new CancellationTokenSource();
        var gateway = new FakeGetItemGateway(GetItemGatewayResult.Found(Item));

        await new GetItemUseCase(gateway).ExecuteAsync(new GetItemRequest("12"), source.Token);

        Assert.Equal(source.Token, gateway.LastCancellationToken);
    }

    [Fact]
    public async Task Lets_the_callers_cancellation_travel_up_unchanged()
    {
        using var source = new CancellationTokenSource();
        var gateway = new FakeGetItemGateway(async (_, token) =>
        {
            await source.CancelAsync();
            token.ThrowIfCancellationRequested();
            return GetItemGatewayResult.Found(Item);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new GetItemUseCase(gateway).ExecuteAsync(new GetItemRequest("12"), source.Token));
    }
}
