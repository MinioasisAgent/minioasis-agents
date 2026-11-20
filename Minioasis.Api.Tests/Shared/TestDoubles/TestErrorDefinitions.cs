using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Minioasis.Api.Shared.Models;
using Minioasis.Application.Shared.Errors;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Minioasis.Api.Tests.Shared.TestDoubles;

/// <summary>
/// Test definitions and messages, added to the real ones through <see cref="ApplicationErrorDefinitionSource"/> and
/// <see cref="ErrorMessageSource"/>.
/// </summary>
public static class TestErrorDefinitions
{
    /// <summary>A capability's client-visible error, with one placeholder.</summary>
    public static readonly ApplicationErrorDefinition NotFound =
        new("test.not_found", ApplicationErrorCategory.NotFound);

    /// <summary>A client-visible error with two placeholders.</summary>
    public static readonly ApplicationErrorDefinition TwoPlaceholders =
        new("test.two_placeholders", ApplicationErrorCategory.Validation);

    public const string NotFoundTitle = "Test item not found";

    public const string NotFoundDetail = "No test item exists with identifier {id}.";

    public const string TwoPlaceholdersTitle = "Invalid test value for {field}";

    public const string TwoPlaceholdersDetail = "The value {value} is not valid for {field}.";

    /// <summary>The real definitions of the Application assembly, and the test definitions.</summary>
    public static ApplicationErrorDefinitionSource Definitions =>
        new([.. ApplicationErrorDefinitionSource.FromAssembly(typeof(ApplicationErrorDefinition).Assembly)
            .Definitions, NotFound, TwoPlaceholders]);

    /// <summary>The real messages of <c>errors.en.json</c>, and the test messages.</summary>
    public static ErrorMessageSource Messages
    {
        get
        {
            var messages = JsonNode.Parse(ErrorMessageSource.FromEmbeddedResource().Content.Span)!.AsObject();
            messages[NotFound.Code] = new JsonObject { ["title"] = NotFoundTitle, ["detail"] = NotFoundDetail };
            messages[TwoPlaceholders.Code] = new JsonObject
            {
                ["title"] = TwoPlaceholdersTitle,
                ["detail"] = TwoPlaceholdersDetail,
            };
            return new ErrorMessageSource(JsonSerializer.SerializeToUtf8Bytes(messages));
        }
    }

    /// <summary>Replaces the definition and message sources with <see cref="Definitions"/> and
    /// <see cref="Messages"/>.</summary>
    public static void Replace(IServiceCollection services)
    {
        services.RemoveAll<ApplicationErrorDefinitionSource>();
        services.AddSingleton(Definitions);
        services.RemoveAll<ErrorMessageSource>();
        services.AddSingleton(Messages);
    }
}
