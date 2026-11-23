using Microsoft.Extensions.Logging;
using Minioasis.Application.Items.Models;
using Minioasis.Application.Items.Outcomes;
using Minioasis.Application.Items.Results;
using Minioasis.Koha.Authentication.Exceptions;
using System.Globalization;
using System.Net;
using Xunit;

namespace Minioasis.Koha.Tests.TestKoha;

/// <summary>
/// The checks of brief 001 against the test Koha (TS-013): behaviors 1, 5, 8, 12 (403 access_denied), 14, and 26.
/// They skip themselves when the five MINIOASIS_TEST_KOHA_* variables are absent. Run them alone with
/// <c>dotnet test minioasis.slnx --filter "Category=TestKoha"</c>.
/// </summary>
[Trait("Category", "TestKoha")]
public sealed class TestKohaChecks
{
    /// <summary>An item number that the test Koha does not have: the largest valid one.</summary>
    private const int AbsentItemNumber = int.MaxValue;

    private static readonly TimeSpan CheckLimit = TimeSpan.FromSeconds(60);

    private readonly TestKohaSettings? settings = TestKohaSettings.FromEnvironment();

    [Fact]
    public async Task Behavior_01_reads_an_existing_item()
    {
        using var host = new TestKohaHost(Settings());
        using var client = host.CreateApiClient();
        await using var record = await TestKohaRecord.CreateAsync(client, Cancellation);

        var result = await GetItemAsync(host, record.ItemId);

        Assert.True(result.IsFound, $"Expected the item, got {result.Outcome}.");
        Assert.Equal(record.ItemId.ToString(CultureInfo.InvariantCulture), result.Item.Id);
        Assert.Equal(record.BiblioId.ToString(CultureInfo.InvariantCulture), result.Item.BibliographicRecordId);
        Assert.Equal(record.Barcode, result.Item.Barcode);
        Assert.Equal(record.CallNumber, result.Item.CallNumber);

        Assert.Equal(HttpStatusCode.NoContent, await record.DeleteItemAsync());
        Assert.Equal(HttpStatusCode.NoContent, await record.DeleteBiblioAsync());
    }

    [Fact]
    public async Task Behavior_05_reports_an_item_that_koha_does_not_have_as_not_found()
    {
        using var host = new TestKohaHost(Settings());
        using var client = host.CreateApiClient();
        await using var record = await TestKohaRecord.CreateAsync(client, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, await record.DeleteItemAsync());

        var deleted = await GetItemAsync(host, record.ItemId);
        var absent = await GetItemAsync(host, AbsentItemNumber);

        Assert.Equal(GetItemOutcome.NotFound, deleted.Outcome);
        Assert.Equal(GetItemOutcome.NotFound, absent.Outcome);
        Assert.Equal(HttpStatusCode.NoContent, await record.DeleteBiblioAsync());
    }

    [Fact]
    public async Task Behavior_08_koha_accepts_the_credentials_in_the_basic_header_with_only_the_grant_type_in_the_body()
    {
        var current = Settings();
        using var host = new TestKohaHost(current);

        var token = await host.Tokens.GetAsync(Cancellation).WaitAsync(CheckLimit, Cancellation);

        Assert.False(string.IsNullOrWhiteSpace(token));
        var exchange = Assert.Single(host.OAuth.Exchanges);
        Assert.Equal(HttpStatusCode.OK, exchange.Status);
        Assert.Equal(HttpMethod.Post, exchange.Method);
        Assert.Equal("Basic", exchange.AuthorizationScheme);
        Assert.Contains("grant_type=client_credentials", exchange.Body!.Split('&'));
        Assert.DoesNotContain(current.ClientSecret, exchange.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(Uri.EscapeDataString(current.ClientSecret), exchange.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Behavior_12_a_wrong_secret_is_refused_with_403_access_denied_and_is_an_authentication_failure()
    {
        var current = Settings();
        using var host = new TestKohaHost(current, clientSecret: current.ClientSecret + "-wrong");

        await Assert.ThrowsAsync<KohaAuthenticationException>(
            () => host.Tokens.GetAsync(Cancellation).WaitAsync(CheckLimit, Cancellation));
        var result = await GetItemAsync(host, AbsentItemNumber);

        Assert.Equal(GetItemOutcome.AuthenticationFailed, result.Outcome);
        Assert.All(host.OAuth.Exchanges, exchange => Assert.Equal(HttpStatusCode.Forbidden, exchange.Status));
        Assert.Empty(host.Api.Exchanges);
        var refused = host.Logs.Entries.First(entry => entry.Level == LogLevel.Error);
        Assert.Equal(403, refused.Values["StatusCode"]);
        Assert.Equal("access_denied", refused.Values["OAuthError"]);
    }

    [Fact]
    public async Task Behavior_14_after_a_401_the_token_is_replaced_and_the_get_is_sent_once_more()
    {
        using var host = new TestKohaHost(Settings(), rewriting: new TokenRewritingHandler(everyAnswer: false));

        var result = await GetItemAsync(host, AbsentItemNumber);

        Assert.Equal(GetItemOutcome.NotFound, result.Outcome);
        Assert.Equal(2, host.OAuth.Exchanges.Count);
        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.NotFound], host.Api.Exchanges.Select(exchange => exchange.Status));
    }

    [Fact]
    public async Task Behavior_26_when_every_token_is_invalid_the_adapter_reports_an_authentication_failure()
    {
        using var host = new TestKohaHost(Settings(), rewriting: new TokenRewritingHandler(everyAnswer: true));

        var result = await GetItemAsync(host, AbsentItemNumber);

        Assert.Equal(GetItemOutcome.AuthenticationFailed, result.Outcome);
        Assert.Equal(2, host.OAuth.Exchanges.Count);
        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized], host.Api.Exchanges.Select(exchange => exchange.Status));
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private TestKohaSettings Settings()
    {
        Assert.SkipWhen(settings is null, TestKohaSettings.SkipReason);
        return settings!;
    }

    private static Task<GetItemGatewayResult> GetItemAsync(TestKohaHost host, int id)
    {
        Assert.True(ItemId.TryCreate(id.ToString(CultureInfo.InvariantCulture), out var itemId));
        return host.CreateGateway().GetItemAsync(itemId, Cancellation).WaitAsync(CheckLimit, Cancellation);
    }
}
