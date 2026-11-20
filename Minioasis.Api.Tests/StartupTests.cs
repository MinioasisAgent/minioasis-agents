using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Minioasis.Api.Shared.Models;
using Minioasis.Api.Tests.Shared.Factories;
using Minioasis.Api.Tests.Shared.TestDoubles;
using Minioasis.Application.Shared.Errors;
using Minioasis.Koha.Shared.Exceptions;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace Minioasis.Api.Tests;

public sealed class StartupTests
{
    [Fact]
    public async Task Real_registry_and_resource_file_are_consistent()
    {
        await using var factory = new ApiTestHostFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/does-not-exist", TestContext.Current.CancellationToken);

        await ProblemResponseAssert.ClientVisibleAsync(
            response, HttpStatusCode.NotFound, "not_found", "Not found", "No resource exists at this address.");
    }

    [Theory]
    [InlineData("a client-visible definition with no message", "not_found")]
    [InlineData("a message file that is not valid JSON", "JSON")]
    [InlineData("an empty registry", "empty")]
    public async Task Inconsistent_definitions_or_messages_stop_the_host(string inconsistency, string named)
    {
        await using var factory = new ApiTestHostFactory(services => Break(services, inconsistency));

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains(named, exception.Message, StringComparison.Ordinal);
    }

    // R10: a blank required Koha variable stops the host before it starts, naming the variable.
    [Theory]
    [InlineData("MINIOASIS_KOHA_BASE_URL", "")]
    [InlineData("MINIOASIS_KOHA_CLIENT_ID", "   ")]
    [InlineData("MINIOASIS_KOHA_CLIENT_SECRET", "")]
    [InlineData("MINIOASIS_KOHA_CLIENT_SECRET", "   ")]
    public async Task Invalid_koha_variable_stops_the_host(string variable, string value)
    {
        await using var factory = new ApiTestHostFactory(
            settings: new Dictionary<string, string?> { [variable] = value });

        var exception = Assert.Throws<KohaConfigurationException>(() => factory.CreateClient());

        Assert.Contains(variable, exception.Message, StringComparison.Ordinal);
    }

    // R11, D5: Program.cs passes the Development flag, so plain http stops the host in Production and starts it in
    // Development.
    [Fact]
    public async Task Http_base_url_fails_in_production_and_starts_in_development()
    {
        var settings = new Dictionary<string, string?> { ["MINIOASIS_KOHA_BASE_URL"] = "http://koha.test/api/v1" };
        await using var production = new ApiTestHostFactory(settings: settings, environment: "Production");
        await using var development = new ApiTestHostFactory(settings: settings, environment: "Development");

        var exception = Assert.Throws<KohaConfigurationException>(() => production.CreateClient());
        using var client = development.CreateClient();
        using var response = await client.GetAsync("/does-not-exist", TestContext.Current.CancellationToken);

        Assert.Contains("MINIOASIS_KOHA_BASE_URL", exception.Message, StringComparison.Ordinal);
        Assert.Contains("https is required", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("koha.test", exception.ToString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // R14, D6: starting the host sends no request through any HTTP client. The recording handler replaces the primary
    // handler of every named client and knows nothing about Koha (TS-003).
    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task Starting_sends_nothing_to_koha(string environment)
    {
        using var recorder = new RecordingHandler();
        await using var factory = new ApiTestHostFactory(
            services => services.ConfigureAll<HttpClientFactoryOptions>(options =>
                options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = recorder)),
            environment: environment);

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/does-not-exist", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, recorder.Count);
    }

    private static void Break(IServiceCollection services, string inconsistency)
    {
        switch (inconsistency)
        {
            case "a client-visible definition with no message":
                var messages = JsonNode.Parse(ErrorMessageSource.FromEmbeddedResource().Content.Span)!.AsObject();
                messages.Remove("not_found");
                Replace(services, new ErrorMessageSource(Encoding.UTF8.GetBytes(messages.ToJsonString())));
                break;
            case "a message file that is not valid JSON":
                Replace(services, new ErrorMessageSource(Encoding.UTF8.GetBytes("{")));
                break;
            case "an empty registry":
                services.RemoveAll<ApplicationErrorDefinitionSource>();
                services.AddSingleton(new ApplicationErrorDefinitionSource([]));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(inconsistency), inconsistency, "Unknown case.");
        }
    }

    private static void Replace(IServiceCollection services, ErrorMessageSource source)
    {
        services.RemoveAll<ErrorMessageSource>();
        services.AddSingleton(source);
    }

    // Records every request it receives and answers each one with 200, so a request at startup is counted rather
    // than sent anywhere.
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private int count;

        public int Count => Volatile.Read(ref count);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref count);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
