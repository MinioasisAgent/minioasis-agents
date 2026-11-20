using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minioasis.Koha.Authentication.Exceptions;
using Minioasis.Koha.Authentication.Providers;
using Minioasis.Koha.Shared.Clients;
using Minioasis.Koha.Shared.Exceptions;
using Minioasis.Koha.Shared.Extensions;
using Minioasis.Koha.Tests.Shared.TestDoubles;
using System.Net;
using Xunit;

namespace Minioasis.Koha.Tests.Shared.Extensions;

/// <summary>
/// Spec 001, R23, built from <c>AddKohaIntegration</c> with every category captured at <c>Trace</c>. Pull request 4
/// covers the token request; the protected call, its 401 retry, and the header redaction follow in pull request 5.
/// </summary>
public sealed class KohaLoggingTests
{
    private const string ClientSecret = "client-secret-5f0c9e";
    private const string AccessToken = "access-token-8d2b71";
    private const string Marker = "koha-body-marker-3a6e";

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private const string SuccessBody =
        $$"""{"access_token":"{{AccessToken}}","token_type":"Bearer","expires_in":3600}""";

    // Failing token replies, each carrying the marker or the access token or both: Koha error bodies under statuses
    // that the contract defines (400, 403) and does not (401, 500, 503), and 200 replies that fail R16 or cannot be
    // parsed.
    private static readonly (int Status, string Body)[] Failures =
    [
        (400, $$"""{"error":"{{Marker}}","error_code":"{{Marker}}"}"""),
        (401, $"{Marker} {AccessToken}"),
        (403, $$"""{"error":"{{Marker}}"}"""),
        (500, $"<html>{Marker} {AccessToken}</html>"),
        (503, $$"""{"error":"{{Marker}}","access_token":"{{AccessToken}}"}"""),
        (200, $$"""{"access_token":"{{AccessToken}}","token_type":"mac","expires_in":3600,"note":"{{Marker}}"}"""),
        (200, $$"""{"access_token":"{{AccessToken}}","token_type":"Bearer","expires_in":"{{Marker}}"}"""),
        (200, $$"""{"access_token":"{{AccessToken}}","note":"{{Marker}}"""),
        (200, $"{Marker} {AccessToken}"),
    ];

    public static TheoryData<int, string> FailingReplies => Data(Failures);

    public static TheoryData<int, string> Replies => Data([(200, SuccessBody), .. Failures]);

    // R23: whatever the token reply, no entry and no scope of any category, HttpClient's included, contains the
    // client secret, the access token, or any part of Koha's body.
    [Theory]
    [MemberData(nameof(Replies))]
    public async Task No_entry_contains_secret_token_header_or_body(int status, string body)
    {
        var logs = new RecordingLoggerProvider();
        using var services = Services(logs, StubHttpMessageHandler.Reply((HttpStatusCode)status, body));

        await Record.ExceptionAsync(() => services.GetRequiredService<KohaAccessTokenProvider>()
            .GetTokenAsync(TestContext.Current.CancellationToken)
            .WaitAsync(Wait, TestContext.Current.CancellationToken));

        // The capture works: HttpClient's own entries for the token request are recorded.
        Assert.Contains(
            logs.Entries,
            entry => entry.Category.StartsWith($"System.Net.Http.HttpClient.{KohaClientNames.OAuth}.", StringComparison.Ordinal));
        Assert.All(logs.Texts, text =>
        {
            Assert.DoesNotContain(ClientSecret, text, StringComparison.Ordinal);
            Assert.DoesNotContain(AccessToken, text, StringComparison.Ordinal);
            Assert.DoesNotContain(Marker, text, StringComparison.Ordinal);
        });
    }

    // R23, plan "Approach": the documented Koha exceptions carry Koha's status, and neither Koha's body, nor the
    // token, nor the generated client's exception (which holds the body).
    [Theory]
    [MemberData(nameof(FailingReplies))]
    public async Task Koha_exceptions_carry_no_body(int status, string body)
    {
        using var services = Services(
            new RecordingLoggerProvider(),
            StubHttpMessageHandler.Reply((HttpStatusCode)status, body));

        var exception = await Record.ExceptionAsync(() => services.GetRequiredService<KohaAccessTokenProvider>()
            .GetTokenAsync(TestContext.Current.CancellationToken)
            .WaitAsync(Wait, TestContext.Current.CancellationToken));

        var kohaStatus = exception switch
        {
            KohaAuthenticationException authentication => authentication.KohaStatus,
            KohaUnavailableException unavailable => unavailable.KohaStatus,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected exception: {exception}"),
        };
        Assert.Equal(status, kohaStatus);
        Assert.Null(exception.InnerException);
        var text = exception.ToString();
        Assert.DoesNotContain(Marker, text, StringComparison.Ordinal);
        Assert.DoesNotContain(AccessToken, text, StringComparison.Ordinal);
        Assert.DoesNotContain(ClientSecret, text, StringComparison.Ordinal);
    }

    private static TheoryData<int, string> Data(IEnumerable<(int Status, string Body)> replies)
    {
        var data = new TheoryData<int, string>();
        foreach (var (status, body) in replies)
        {
            data.Add(status, body);
        }

        return data;
    }

    private static ServiceProvider Services(RecordingLoggerProvider logs, HttpResponseMessage reply)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MINIOASIS_KOHA_BASE_URL"] = "https://koha.test/api/v1",
                ["MINIOASIS_KOHA_CLIENT_ID"] = "dummy-client-id",
                ["MINIOASIS_KOHA_CLIENT_SECRET"] = ClientSecret,
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        services.AddKohaIntegration(configuration, allowInsecureHttp: false);
        services.AddHttpClient(KohaClientNames.OAuth)
            .ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler(() => reply));
        return services.BuildServiceProvider();
    }
}
