using Microsoft.Extensions.Logging;
using Minioasis.Application.Items.Models;
using Minioasis.Application.Items.Outcomes;
using Minioasis.Application.Items.Results;
using Minioasis.Koha.Tests.Shared.TestDoubles;
using System.Globalization;
using System.Net;
using Xunit;

namespace Minioasis.Koha.Tests.Items.Adapters;

public sealed class KohaGetItemAdapterTests
{
    [Fact]
    public async Task Returns_the_identifiers_barcode_and_call_number_of_an_existing_item()
    {
        using var host = new KohaTestHost(api: Answer(() => KohaResponses.Item(12, 7, "BC-0001", "QA 76.73")));

        var result = await GetItemAsync(host, 12);

        Assert.True(result.IsFound);
        Assert.Equal("12", result.Item.Id);
        Assert.Equal("7", result.Item.BibliographicRecordId);
        Assert.Equal("BC-0001", result.Item.Barcode);
        Assert.Equal("QA 76.73", result.Item.CallNumber);
        var request = Assert.Single(host.Api.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri("https://koha.test/api/v1/items/12"), request.Uri);
    }

    [Fact]
    public async Task Returns_null_for_a_missing_barcode_and_call_number()
    {
        using var host = new KohaTestHost(api: Answer(() => KohaResponses.Item(12, 7, null, null)));

        var result = await GetItemAsync(host, 12);

        Assert.True(result.IsFound);
        Assert.Null(result.Item.Barcode);
        Assert.Null(result.Item.CallNumber);
    }

    [Fact]
    public async Task Returns_null_for_a_barcode_and_call_number_that_koha_leaves_out()
    {
        using var host = new KohaTestHost(api: Answer(() => KohaResponses.Json(HttpStatusCode.OK, """{"item_id":12,"biblio_id":7}""")));

        var result = await GetItemAsync(host, 12);

        Assert.True(result.IsFound);
        Assert.Null(result.Item.Barcode);
        Assert.Null(result.Item.CallNumber);
    }

    [Fact]
    public async Task Reads_the_item_the_same_way_when_the_culture_is_not_English()
    {
        using var host = new KohaTestHost(api: Answer(() => KohaResponses.Item(1234567, 7654321, "BC-1", "QA 76")));
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = NonEnglishCulture();

            var result = await GetItemAsync(host, 1234567);

            Assert.True(result.IsFound);
            Assert.Equal("1234567", result.Item.Id);
            Assert.Equal("7654321", result.Item.BibliographicRecordId);
            Assert.Equal(new Uri("https://koha.test/api/v1/items/1234567"), Assert.Single(host.Api.Requests).Uri);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task Reports_a_404_with_the_error_code_not_found_as_not_found()
    {
        using var host = new KohaTestHost(api: Answer(KohaResponses.ItemNotFound));

        var result = await GetItemAsync(host, 12);

        Assert.Equal(GetItemOutcome.NotFound, result.Outcome);
    }

    public static TheoryData<string, Func<HttpResponseMessage>, GetItemOutcome> KohaAnswers => new()
    {
        { "401 twice", () => KohaResponses.Json(HttpStatusCode.Unauthorized, """{"error":"Invalid token"}""", KohaResponses.Utf8WithoutHyphen), GetItemOutcome.AuthenticationFailed },
        { "403", () => KohaResponses.Json(HttpStatusCode.Forbidden, """{"error":"Authorization failure. Missing required permission(s).","required_permissions":{"catalogue":"1"}}""", KohaResponses.Utf8WithoutHyphen), GetItemOutcome.AuthenticationFailed },
        { "429", () => KohaResponses.Status(HttpStatusCode.TooManyRequests), GetItemOutcome.Unavailable },
        { "500", () => KohaResponses.Json(HttpStatusCode.InternalServerError, """{"error":"Something went wrong","error_code":"internal_server_error"}"""), GetItemOutcome.Unavailable },
        { "502", () => KohaResponses.Json(HttpStatusCode.BadGateway, "<html>Bad gateway</html>", "text/html"), GetItemOutcome.Unavailable },
        { "503", () => KohaResponses.Json(HttpStatusCode.ServiceUnavailable, """{"error":"Under maintenance"}"""), GetItemOutcome.Unavailable },
        { "504", () => KohaResponses.Status(HttpStatusCode.GatewayTimeout), GetItemOutcome.Unavailable },
        { "599", () => KohaResponses.Status((HttpStatusCode)599), GetItemOutcome.Unavailable },
        { "204", () => KohaResponses.Status(HttpStatusCode.NoContent), GetItemOutcome.InvalidResponse },
        { "300", () => KohaResponses.Status(HttpStatusCode.MultipleChoices), GetItemOutcome.InvalidResponse },
        { "301", () => Redirect(HttpStatusCode.MovedPermanently), GetItemOutcome.InvalidResponse },
        { "302", () => Redirect(HttpStatusCode.Found), GetItemOutcome.InvalidResponse },
        { "308", () => Redirect(HttpStatusCode.PermanentRedirect), GetItemOutcome.InvalidResponse },
        { "400", () => KohaResponses.Json(HttpStatusCode.BadRequest, """{"errors":[{"message":"Expected integer - got string.","path":"/item_id"}],"status":400}"""), GetItemOutcome.InvalidResponse },
        { "409", () => KohaResponses.Json(HttpStatusCode.Conflict, """{"error":"Conflict"}"""), GetItemOutcome.InvalidResponse },
        { "418", () => KohaResponses.Status((HttpStatusCode)418), GetItemOutcome.InvalidResponse },
        { "600", () => KohaResponses.Status((HttpStatusCode)600), GetItemOutcome.InvalidResponse },
        { "404 HTML", () => KohaResponses.Json(HttpStatusCode.NotFound, "<html><body>Page not found</body></html>", "text/html; charset=UTF-8"), GetItemOutcome.InvalidResponse },
        { "404 other error_code", () => KohaResponses.Json(HttpStatusCode.NotFound, """{"error":"Biblio not found","error_code":"biblio_not_found"}"""), GetItemOutcome.InvalidResponse },
        { "404 without error_code", () => KohaResponses.Json(HttpStatusCode.NotFound, """{"error":"Not found"}"""), GetItemOutcome.InvalidResponse },
        { "404 empty", () => KohaResponses.Status(HttpStatusCode.NotFound), GetItemOutcome.InvalidResponse },
        { "200 unreadable", () => KohaResponses.Json(HttpStatusCode.OK, "<html>maintenance</html>", "text/html"), GetItemOutcome.InvalidResponse },
        { "200 truncated JSON", () => KohaResponses.Json(HttpStatusCode.OK, """{"item_id":12,"biblio_id":"""), GetItemOutcome.InvalidResponse },
        { "200 null", () => KohaResponses.Json(HttpStatusCode.OK, "null"), GetItemOutcome.InvalidResponse },
        { "200 array", () => KohaResponses.Json(HttpStatusCode.OK, "[]"), GetItemOutcome.InvalidResponse },
        { "200 wrong field type", () => KohaResponses.Json(HttpStatusCode.OK, """{"item_id":"twelve","biblio_id":7}"""), GetItemOutcome.InvalidResponse },
        { "200 item_id 0", () => KohaResponses.Item(0, 7, "BC-1", "QA 76"), GetItemOutcome.InvalidResponse },
        { "200 item_id negative", () => KohaResponses.Item(-12, 7, "BC-1", "QA 76"), GetItemOutcome.InvalidResponse },
        { "200 biblio_id 0", () => KohaResponses.Item(12, 0, "BC-1", "QA 76"), GetItemOutcome.InvalidResponse },
        { "200 biblio_id missing", () => KohaResponses.Json(HttpStatusCode.OK, """{"item_id":12,"external_id":"BC-1"}"""), GetItemOutcome.InvalidResponse },
        { "200 item_id missing", () => KohaResponses.Json(HttpStatusCode.OK, """{"biblio_id":7,"external_id":"BC-1"}"""), GetItemOutcome.InvalidResponse },
    };

    [Theory]
    [MemberData(nameof(KohaAnswers))]
    public async Task Translates_each_answer_of_koha_into_its_outcome(
        string name,
        Func<HttpResponseMessage> answer,
        GetItemOutcome expected)
    {
        using var host = new KohaTestHost(api: Answer(answer));

        var result = await GetItemAsync(host, 12);

        Assert.True(expected == result.Outcome, $"{name}: expected {expected}, got {result.Outcome}.");
    }

    [Fact]
    public async Task Sends_the_get_twice_and_reports_authentication_failure_after_401_twice()
    {
        using var host = new KohaTestHost(api: Answer(() =>
            KohaResponses.Json(HttpStatusCode.Unauthorized, """{"error":"Invalid token"}""", KohaResponses.Utf8WithoutHyphen)));

        var result = await GetItemAsync(host, 12);

        Assert.Equal(GetItemOutcome.AuthenticationFailed, result.Outcome);
        Assert.Equal(2, host.Api.Requests.Count);
    }

    [Fact]
    public async Task Reports_an_item_found_after_one_401_and_a_new_token()
    {
        var answers = new Queue<HttpResponseMessage>(
        [
            KohaResponses.Json(HttpStatusCode.Unauthorized, """{"error":"Token expired"}""", KohaResponses.Utf8WithoutHyphen),
            KohaResponses.Item(12, 7, "BC-1", "QA 76"),
        ]);
        using var host = new KohaTestHost(api: Answer(answers.Dequeue));

        var result = await GetItemAsync(host, 12);

        Assert.True(result.IsFound);
        Assert.Equal(2, host.OAuth.Requests.Count);
    }

    [Fact]
    public async Task Reports_a_failure_to_get_a_token_as_authentication_failure_without_calling_for_the_item()
    {
        using var host = new KohaTestHost(oauth: (_, _) => Task.FromResult(
            KohaResponses.Json(HttpStatusCode.Forbidden, """{"error":"access_denied"}""", KohaResponses.Utf8WithoutHyphen)));

        var result = await GetItemAsync(host, 12);

        Assert.Equal(GetItemOutcome.AuthenticationFailed, result.Outcome);
        Assert.Empty(host.Api.Requests);
    }

    [Fact]
    public async Task Reports_a_connection_failure_as_unavailable()
    {
        using var host = new KohaTestHost(api: (_, _) => Task.FromException<HttpResponseMessage>(
            new HttpRequestException(HttpRequestError.ConnectionError, "Connection refused.")));

        var result = await GetItemAsync(host, 12);

        Assert.Equal(GetItemOutcome.Unavailable, result.Outcome);
    }

    [Fact]
    public async Task Reports_a_response_that_is_cut_off_as_unavailable()
    {
        using var host = new KohaTestHost(api: Answer(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new CutOffStream("""{"item_id":12,"bibl""")),
        }));

        var result = await GetItemAsync(host, 12);

        Assert.Equal(GetItemOutcome.Unavailable, result.Outcome);
    }

    [Fact]
    public async Task Reports_the_deadline_expiring_while_the_item_is_requested_as_unavailable()
    {
        using var host = new KohaTestHost(api: async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return KohaResponses.Item(12, 7, null, null);
        });

        var pending = GetItemTaskAsync(host, 12, CancellationToken.None);
        await Wait.UntilAsync(() => host.Api.Requests.Count == 1);
        host.Time.Advance(TimeSpan.FromSeconds(10));

        var result = await pending.WaitAsync(Wait.Limit, TestContext.Current.CancellationToken);
        Assert.Equal(GetItemOutcome.Unavailable, result.Outcome);
    }

    [Fact]
    public async Task Reports_the_deadline_expiring_while_the_token_is_requested_as_authentication_failure()
    {
        using var host = new KohaTestHost(oauth: async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return KohaResponses.Token();
        });

        var pending = GetItemTaskAsync(host, 12, CancellationToken.None);
        await Wait.UntilAsync(() => host.OAuth.Requests.Count == 1);
        await host.Time.WaitForPendingTimersAsync(2);
        host.Time.Advance(TimeSpan.FromSeconds(10));

        var result = await pending.WaitAsync(Wait.Limit, TestContext.Current.CancellationToken);
        Assert.Equal(GetItemOutcome.AuthenticationFailed, result.Outcome);
        Assert.Empty(host.Api.Requests);
    }

    [Fact]
    public async Task One_deadline_of_10_seconds_covers_the_token_the_request_and_the_retry()
    {
        var tokenGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var issued = 0;
        var calls = 0;
        using var host = new KohaTestHost(
            oauth: async (_, _) =>
            {
                if (Interlocked.Increment(ref issued) == 1)
                {
                    await tokenGate.Task;
                }

                return KohaResponses.Token(token: $"token-{issued}");
            },
            api: async (_, token) =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    return KohaResponses.Json(HttpStatusCode.Unauthorized, "{}", KohaResponses.Utf8WithoutHyphen);
                }

                await Task.Delay(Timeout.Infinite, token);
                return KohaResponses.Item(12, 7, null, null);
            });

        var pending = GetItemTaskAsync(host, 12, CancellationToken.None);
        await Wait.UntilAsync(() => host.OAuth.Requests.Count == 1);
        host.Time.Advance(TimeSpan.FromSeconds(6));
        tokenGate.SetResult();
        await Wait.UntilAsync(() => host.Api.Requests.Count == 2);

        host.Time.Advance(TimeSpan.FromSeconds(4) - TimeSpan.FromTicks(1));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(pending.IsCompleted);

        host.Time.Advance(TimeSpan.FromTicks(1));
        var result = await pending.WaitAsync(Wait.Limit, TestContext.Current.CancellationToken);
        Assert.Equal(GetItemOutcome.Unavailable, result.Outcome);
    }

    [Fact]
    public async Task Lets_the_callers_cancellation_during_the_item_request_travel_up_and_logs_no_failure()
    {
        using var host = new KohaTestHost(api: async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return KohaResponses.Item(12, 7, null, null);
        });
        using var caller = new CancellationTokenSource();

        var pending = GetItemTaskAsync(host, 12, caller.Token);
        await Wait.UntilAsync(() => host.Api.Requests.Count == 1);
        await caller.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pending.WaitAsync(Wait.Limit, TestContext.Current.CancellationToken));
        Assert.DoesNotContain(host.Logs.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Lets_the_callers_cancellation_during_the_token_request_travel_up_and_logs_no_failure()
    {
        using var host = new KohaTestHost(oauth: async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return KohaResponses.Token();
        });
        using var caller = new CancellationTokenSource();

        var pending = GetItemTaskAsync(host, 12, caller.Token);
        await Wait.UntilAsync(() => host.OAuth.Requests.Count == 1);
        await caller.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pending.WaitAsync(Wait.Limit, TestContext.Current.CancellationToken));
        Assert.DoesNotContain(host.Logs.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Theory]
    [InlineData("https://koha.test")]
    [InlineData("https://koha.test/")]
    public async Task Sends_the_requests_to_the_base_url_with_api_v1_added_after_one_slash(string baseUrl)
    {
        using var host = new KohaTestHost(baseUrl: baseUrl);

        await GetItemAsync(host, 12);

        Assert.Equal(new Uri("https://koha.test/api/v1/oauth/token"), Assert.Single(host.OAuth.Requests).Uri);
        Assert.Equal(new Uri("https://koha.test/api/v1/items/12"), Assert.Single(host.Api.Requests).Uri);
    }

    [Fact]
    public async Task Sends_the_requests_to_a_loopback_base_url_with_its_port()
    {
        using var host = new KohaTestHost(baseUrl: "http://localhost:8081");

        await GetItemAsync(host, 12);

        Assert.Equal("http://localhost:8081/api/v1/oauth/token", Assert.Single(host.OAuth.Requests).Uri.AbsoluteUri);
        Assert.Equal("http://localhost:8081/api/v1/items/12", Assert.Single(host.Api.Requests).Uri.AbsoluteUri);
    }

    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Answer(
        Func<HttpResponseMessage> answer) =>
        (_, _) => Task.FromResult(answer());

    private static HttpResponseMessage Redirect(HttpStatusCode status)
    {
        var response = new HttpResponseMessage(status);
        response.Headers.Location = new Uri("https://koha.test/cgi-bin/koha/mainpage.pl");
        return response;
    }

    private static Task<GetItemGatewayResult> GetItemAsync(KohaTestHost host, int id) =>
        GetItemTaskAsync(host, id, TestContext.Current.CancellationToken).WaitAsync(Wait.Limit, TestContext.Current.CancellationToken);

    private static Task<GetItemGatewayResult> GetItemTaskAsync(KohaTestHost host, int id, CancellationToken cancellationToken)
    {
        Assert.True(ItemId.TryCreate(id.ToString(CultureInfo.InvariantCulture), out var itemId));
        return host.CreateGateway().GetItemAsync(itemId, cancellationToken);
    }

    private static CultureInfo NonEnglishCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberGroupSeparator = ".";
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NegativeSign = "~";
        culture.DateTimeFormat.DateSeparator = ".";
        return culture;
    }

    /// <summary>A body that ends with an I/O error part of the way through, as when the connection drops.</summary>
    private sealed class CutOffStream(string start) : MemoryStream(System.Text.Encoding.UTF8.GetBytes(start))
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await base.ReadAsync(buffer, cancellationToken);
            return read > 0 ? read : throw new IOException("The response ended prematurely.");
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }
}
