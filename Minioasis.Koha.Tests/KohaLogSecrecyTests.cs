using Minioasis.Application.Items.Models;
using Minioasis.Koha.Tests.Shared.TestDoubles;
using System.Globalization;
using System.Net;
using System.Text;
using Xunit;

namespace Minioasis.Koha.Tests;

/// <summary>
/// No log entry, at any level and from any component, holds the client secret, an access token, or the value of
/// an Authorization header (ER-008), on the success path and on each failure path of brief 001, behaviors 6 and 12.
/// </summary>
public sealed class KohaLogSecrecyTests
{
    private const string Token = "tok-5f0c9a17e2";

    private static readonly string BasicCredentials =
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{KohaTestHost.ClientId}:{KohaTestHost.ClientSecret}"));

    private static Task<HttpResponseMessage> TokenAnswer() =>
        Task.FromResult(KohaResponses.Token(token: Token));

    public static TheoryData<string> Scenarios => [.. ScenarioHosts.Keys];

    private static readonly Dictionary<string, Func<KohaTestHost>> ScenarioHosts = new()
    {
        ["success"] = () => Api(() => KohaResponses.Item(12, 7, "BC-1", "QA 76")),
        ["not found"] = () => Api(KohaResponses.ItemNotFound),
        ["401 twice"] = () => Api(() => KohaResponses.Json(HttpStatusCode.Unauthorized, """{"error":"Invalid token"}""", KohaResponses.Utf8WithoutHyphen)),
        ["403"] = () => Api(() => KohaResponses.Json(HttpStatusCode.Forbidden, """{"error":"Forbidden"}""", KohaResponses.Utf8WithoutHyphen)),
        ["429"] = () => Api(() => KohaResponses.Status(HttpStatusCode.TooManyRequests)),
        ["500"] = () => Api(() => KohaResponses.Json(HttpStatusCode.InternalServerError, """{"error":"Something went wrong"}""")),
        ["599"] = () => Api(() => KohaResponses.Status((HttpStatusCode)599)),
        ["204"] = () => Api(() => KohaResponses.Status(HttpStatusCode.NoContent)),
        ["301"] = () => Api(() => KohaResponses.Status(HttpStatusCode.MovedPermanently)),
        ["400"] = () => Api(() => KohaResponses.Json(HttpStatusCode.BadRequest, """{"errors":[]}""")),
        ["404 HTML"] = () => Api(() => KohaResponses.Json(HttpStatusCode.NotFound, "<html></html>", "text/html")),
        ["200 unreadable"] = () => Api(() => KohaResponses.Json(HttpStatusCode.OK, "<html></html>", "text/html")),
        ["200 item_id 0"] = () => Api(() => KohaResponses.Item(0, 7, null, null)),
        ["item connection failure"] = () => new KohaTestHost(
            oauth: (_, _) => TokenAnswer(),
            api: (_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException(HttpRequestError.ConnectionError, "Connection refused."))),
        ["token 403 access_denied"] = () => OAuth(() => KohaResponses.Json(HttpStatusCode.Forbidden, """{"error":"access_denied"}""", KohaResponses.Utf8WithoutHyphen)),
        ["token 500"] = () => OAuth(() => KohaResponses.Json(HttpStatusCode.InternalServerError, "{}")),
        ["token unreadable"] = () => OAuth(() => KohaResponses.Json(HttpStatusCode.OK, "<html></html>", "text/html")),
        ["token rejected"] = () => OAuth(() => KohaResponses.Token(token: Token, tokenType: "MAC")),
        ["token connection failure"] = () => new KohaTestHost(
            oauth: (_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException(HttpRequestError.ConnectionError, "Connection refused."))),
        ["token timeout"] = () => new KohaTestHost(
            oauth: (_, _) => Task.FromException<HttpResponseMessage>(new TaskCanceledException("Timed out.", new TimeoutException()))),
    };

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task No_log_entry_holds_the_secret_a_token_or_an_authorization_value(string scenario)
    {
        using var host = ScenarioHosts[scenario]();
        Assert.True(ItemId.TryCreate(12.ToString(CultureInfo.InvariantCulture), out var itemId));

        await host.CreateGateway().GetItemAsync(itemId, TestContext.Current.CancellationToken)
            .WaitAsync(Wait.Limit, TestContext.Current.CancellationToken);

        Assert.NotEmpty(host.Logs.Entries);
        Assert.All(host.Logs.Entries, entry =>
        {
            Assert.DoesNotContain(KohaTestHost.ClientSecret, entry.AllText, StringComparison.Ordinal);
            Assert.DoesNotContain(Token, entry.AllText, StringComparison.Ordinal);
            Assert.DoesNotContain(BasicCredentials, entry.AllText, StringComparison.Ordinal);
            Assert.DoesNotContain("Bearer " + Token, entry.AllText, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task The_deadline_expiring_while_the_token_is_requested_logs_no_secret()
    {
        using var host = new KohaTestHost(oauth: async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return KohaResponses.Token(token: Token);
        });
        Assert.True(ItemId.TryCreate("12", out var itemId));

        var pending = host.CreateGateway().GetItemAsync(itemId, CancellationToken.None);
        await Wait.UntilAsync(() => host.OAuth.Requests.Count == 1);
        await host.Time.WaitForPendingTimersAsync(2);
        host.Time.Advance(TimeSpan.FromSeconds(10));
        await pending.WaitAsync(Wait.Limit, TestContext.Current.CancellationToken);
        await Wait.UntilAsync(() => host.Logs.Entries.Any(entry => entry.Level >= Microsoft.Extensions.Logging.LogLevel.Error));

        Assert.All(host.Logs.Entries, entry =>
        {
            Assert.DoesNotContain(KohaTestHost.ClientSecret, entry.AllText, StringComparison.Ordinal);
            Assert.DoesNotContain(BasicCredentials, entry.AllText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task The_deadline_expiring_while_the_item_is_requested_logs_no_secret_or_token()
    {
        using var host = new KohaTestHost(
            oauth: (_, _) => TokenAnswer(),
            api: async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return KohaResponses.Item(12, 7, null, null);
            });
        Assert.True(ItemId.TryCreate("12", out var itemId));

        var pending = host.CreateGateway().GetItemAsync(itemId, CancellationToken.None);
        await Wait.UntilAsync(() => host.Api.Requests.Count == 1);
        host.Time.Advance(TimeSpan.FromSeconds(10));
        await pending.WaitAsync(Wait.Limit, TestContext.Current.CancellationToken);

        Assert.All(host.Logs.Entries, entry =>
        {
            Assert.DoesNotContain(KohaTestHost.ClientSecret, entry.AllText, StringComparison.Ordinal);
            Assert.DoesNotContain(Token, entry.AllText, StringComparison.Ordinal);
            Assert.DoesNotContain(BasicCredentials, entry.AllText, StringComparison.Ordinal);
        });
    }

    private static KohaTestHost Api(Func<HttpResponseMessage> answer) =>
        new(oauth: (_, _) => TokenAnswer(), api: (_, _) => Task.FromResult(answer()));

    private static KohaTestHost OAuth(Func<HttpResponseMessage> answer) =>
        new(oauth: (_, _) => Task.FromResult(answer()));
}
