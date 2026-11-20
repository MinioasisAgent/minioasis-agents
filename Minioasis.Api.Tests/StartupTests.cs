using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Minioasis.Api.Shared.Models;
using Minioasis.Api.Tests.Shared.Factories;
using Minioasis.Api.Tests.Shared.TestDoubles;
using Minioasis.Application.Shared.Errors;
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
}
