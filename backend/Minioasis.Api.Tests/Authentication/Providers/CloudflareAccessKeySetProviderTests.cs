using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Minioasis.Api.Tests.Authentication.TestDoubles;
using Minioasis.Api.Tests.Items.TestDoubles;
using Minioasis.Api.Tests.Shared;
using Minioasis.Api.Tests.Shared.TestDoubles;
using Minioasis.Application.Items.Abstractions;
using Minioasis.Application.Items.Responses;
using Minioasis.Application.Items.Results;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Minioasis.Api.Tests.Authentication.Providers;

/// <summary>
/// Brief 002, behaviors 10 to 13: fetching, holding, and refreshing Cloudflare's signing keys, in the application as
/// it is really configured, with only Cloudflare's key address and Koha replaced.
/// </summary>
public sealed class CloudflareAccessKeySetProviderTests
{
    private const string KeysUnavailable = "access.keys_unavailable";

    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    public static TheoryData<string> Tokens => new() { "valid", "invalid", "none" };

    public static TheoryData<string, string> FailedFetches
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var failure in new[] { "connection", "status 500", "status 404", "not a key set", "a JSON array", "empty key set", "no RS256 key" })
            {
                foreach (var token in new[] { "valid", "invalid", "none" })
                {
                    data.Add(failure, token);
                }
            }

            return data;
        }
    }

    [Theory]
    [InlineData("https://example.cloudflareaccess.com", "https://example.cloudflareaccess.com/cdn-cgi/access/certs")]
    [InlineData("https://example.cloudflareaccess.com/", "https://example.cloudflareaccess.com/cdn-cgi/access/certs")]
    [InlineData("https://Example.CloudflareAccess.com/", "https://example.cloudflareaccess.com/cdn-cgi/access/certs")]
    [InlineData("https://localhost:8443", "https://localhost:8443/cdn-cgi/access/certs")]
    public async Task Behavior_10_the_keys_are_fetched_from_the_team_domain_on_the_first_request_and_held(
        string teamDomain,
        string address)
    {
        await using var factory = HostWith(new Dictionary<string, string?> { ["MINIOASIS_CLOUDFLARE_ACCESS_TEAM_DOMAIN"] = teamDomain });
        var issuer = new Uri(address).GetLeftPart(UriPartial.Authority);
        _ = factory.Server;

        Assert.Empty(factory.KeySet.Requests);
        using var first = await AccessRequest.SendAsync(factory, StaffTokens.Create(payload: claims => claims["iss"] = issuer));
        using var second = await AccessRequest.SendAsync(factory, StaffTokens.Create(payload: claims => claims["iss"] = issuer));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal([address], factory.KeySet.Requests.Select(uri => uri.AbsoluteUri));
    }

    [Fact]
    public async Task Behavior_10_a_request_without_a_token_also_fetches_the_keys_once()
    {
        await using var factory = HostWith();

        using var first = await AccessRequest.SendAsync(factory, token: null);
        using var second = await AccessRequest.SendAsync(factory, StaffTokens.Create());

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Single(factory.KeySet.Requests);
    }

    [Fact]
    public async Task Behavior_11_after_the_keys_change_a_token_with_the_new_key_is_accepted_and_the_withdrawn_key_is_refused()
    {
        await using var factory = HostWith();
        var next = new TestSigningKey("next-key");

        using var before = await AccessRequest.SendAsync(factory, StaffTokens.Create());
        factory.KeySet.Publish(next);
        using var withNewKey = await AccessRequest.SendAsync(factory, StaffTokens.Create(next));
        using var withWithdrawnKey = await AccessRequest.SendAsync(factory, StaffTokens.Create());

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withNewKey.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, withWithdrawnKey.StatusCode);
        Assert.Equal(2, factory.KeySet.Requests.Count);
    }

    [Theory]
    [MemberData(nameof(FailedFetches))]
    public async Task Behavior_12_while_no_keys_can_be_fetched_every_request_returns_a_masked_503_and_the_log_holds_the_code(
        string failure,
        string token)
    {
        await using var factory = HostWith();
        factory.KeySet.AnswerWith(Failure(failure));

        using var response = await AccessRequest.SendAsync(factory, TokenOfKind(token));

        await AssertKeysUnavailableAsync(factory, response);
    }

    [Theory]
    [MemberData(nameof(Tokens))]
    public async Task Behavior_12_a_fetch_that_takes_longer_than_10_seconds_returns_a_masked_503(string token)
    {
        var clock = new FakeTimeProvider();
        await using var factory = HostWith(configureServices: services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        });
        factory.KeySet.AnswerWith(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable: the delay ends only by cancellation.");
        });

        var sending = AccessRequest.SendAsync(factory, TokenOfKind(token));
        await clock.WaitForPendingTimersAsync(1);
        clock.Advance(TimeSpan.FromSeconds(9.9));
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(sending.IsCompleted, "The fetch ended before its 10 seconds.");
        clock.Advance(TimeSpan.FromSeconds(0.1));
        using var response = await sending.WaitAsync(Limit, TestContext.Current.CancellationToken);

        await AssertKeysUnavailableAsync(factory, response);
    }

    [Fact]
    public async Task Behavior_12_once_a_fetch_succeeds_a_valid_token_is_accepted()
    {
        await using var factory = HostWith();
        factory.KeySet.AnswerWith(Failure("status 500"));

        using var failed = await AccessRequest.SendAsync(factory, StaffTokens.Create());
        factory.KeySet.Publish(TestSigningKey.Default);
        using var accepted = await AccessRequest.SendAsync(factory, StaffTokens.Create());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task Behavior_12_a_request_that_is_cancelled_while_it_waits_for_the_keys_gets_no_503()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var records = new ConcurrentDictionary<string, int>();
        var started = new ConcurrentDictionary<string, TaskCompletionSource>();
        var finished = new ConcurrentDictionary<string, TaskCompletionSource>();
        foreach (var path in new[] { "/api/v1/items/1", "/api/v1/items/2" })
        {
            started[path] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            finished[path] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        await using var factory = HostWith(configureServices: services =>
            services.AddSingleton<IStartupFilter>(new StatusRecordingStartupFilter(records, started, finished)));
        factory.KeySet.AnswerWith(async (_, _) =>
        {
            await release.Task;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });
        using var fetching = new CancellationTokenSource();
        using var waiting = new CancellationTokenSource();

        // The first request makes the fetch itself; the second waits for it.
        var first = AccessRequest.SendAsync(factory, StaffTokens.Create(), path: "/api/v1/items/1", cancellationToken: fetching.Token);
        await Wait.UntilAsync(() => factory.KeySet.Requests.Count == 1);
        var second = AccessRequest.SendAsync(factory, StaffTokens.Create(), path: "/api/v1/items/2", cancellationToken: waiting.Token);
        await started["/api/v1/items/2"].Task.WaitAsync(Limit, TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        await fetching.CancelAsync();
        await waiting.CancelAsync();
        release.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        foreach (var done in finished.Values)
        {
            await done.Task.WaitAsync(Limit, TestContext.Current.CancellationToken);
        }

        Assert.DoesNotContain(records.Values, status => status is StatusCodes.Status503ServiceUnavailable or StatusCodes.Status500InternalServerError);
        Assert.DoesNotContain(factory.Logs.Entries, entry =>
            entry.Level >= LogLevel.Error && !entry.Category.EndsWith("CloudflareAccessKeySetProvider", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Behavior_13_after_a_failed_refresh_the_held_keys_are_still_accepted_and_the_failure_is_logged()
    {
        await using var factory = HostWith();

        using var before = await AccessRequest.SendAsync(factory, StaffTokens.Create());
        factory.KeySet.AnswerWith(Failure("status 500"));
        using var withUnknownKey = await AccessRequest.SendAsync(factory, StaffTokens.Create(new TestSigningKey("next-key")));
        using var withHeldKey = await AccessRequest.SendAsync(factory, StaffTokens.Create());

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, withUnknownKey.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withHeldKey.StatusCode);
        Assert.Equal(2, factory.KeySet.Requests.Count);
        var logged = Assert.Single(factory.Logs.Entries, entry => entry.Values.TryGetValue("ErrorCode", out var code) && Equals(code, KeysUnavailable));
        Assert.Equal(LogLevel.Error, logged.Level);
    }

    private static ApiTestHostFactory HostWith(
        Dictionary<string, string?>? settings = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var gateway = new FakeGetItemGateway(GetItemGatewayResult.Found(new GetItemResponse("12", "7", "BC-1", "QA 76")));
        return new ApiTestHostFactory(
            services =>
            {
                services.RemoveAll<IGetItemGateway>();
                services.AddSingleton<IGetItemGateway>(gateway);
                configureServices?.Invoke(services);
            },
            settings);
    }

    private static string? TokenOfKind(string kind) => kind switch
    {
        "valid" => StaffTokens.Create(),
        "invalid" => "not-a-token",
        _ => null,
    };

    private static Func<HttpRequestMessage, HttpResponseMessage> Failure(string kind) => kind switch
    {
        "connection" => _ => throw new HttpRequestException(HttpRequestError.ConnectionError, "Connection refused."),
        "status 500" => _ => KeySetHandler.Json(TestSigningKey.KeySet(TestSigningKey.Default), HttpStatusCode.InternalServerError),
        "status 404" => _ => KeySetHandler.Json(TestSigningKey.KeySet(TestSigningKey.Default), HttpStatusCode.NotFound),
        "not a key set" => _ => KeySetHandler.Json("<html><body>Sign in</body></html>"),
        "a JSON array" => _ => KeySetHandler.Json("[1, 2, 3]"),
        "empty key set" => _ => KeySetHandler.Json("""{"keys":[]}"""),
        "no RS256 key" => _ => KeySetHandler.Json(JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                TestSigningKey.Default.ToJsonWebKey(algorithm: "RS512"),
                TestSigningKey.Default.ToJsonWebKey(algorithm: "RS256", keyType: "EC"),
            },
        })),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown failure."),
    };

    /// <summary>
    /// The 503, its log line, and one fetch only: the keys are checked first, so nothing else asks for them again.
    /// </summary>
    private static async Task AssertKeysUnavailableAsync(ApiTestHostFactory factory, HttpResponseMessage response)
    {
        Assert.Single(factory.KeySet.Requests);
        var body = await ProblemAssert.IsMaskedAsync(response, HttpStatusCode.ServiceUnavailable);
        var traceId = body.GetProperty("traceId").GetString();
        Assert.Contains(factory.Logs.Entries, entry =>
            entry.Level == LogLevel.Error
            && entry.Values.TryGetValue("ErrorCode", out var code) && Equals(code, KeysUnavailable)
            && entry.Values.TryGetValue("TraceId", out var loggedTraceId) && Equals(loggedTraceId, traceId));
    }

    /// <summary>Records the status that the server wrote for each path, as the outermost middleware sees it.</summary>
    private sealed class StatusRecordingStartupFilter(
        ConcurrentDictionary<string, int> records,
        ConcurrentDictionary<string, TaskCompletionSource> started,
        ConcurrentDictionary<string, TaskCompletionSource> finished) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                var path = context.Request.Path.Value!;
                started.GetValueOrDefault(path)?.TrySetResult();
                try
                {
                    await nextMiddleware(context);
                }
                finally
                {
                    records[path] = context.Response.StatusCode;
                    finished.GetValueOrDefault(path)?.TrySetResult();
                }
            });
            next(app);
        };
    }
}
