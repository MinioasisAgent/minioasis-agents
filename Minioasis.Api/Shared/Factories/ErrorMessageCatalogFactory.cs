using Minioasis.Api.Shared.Models;
using Minioasis.Application.Shared.Errors;
using System.Text.Json;

namespace Minioasis.Api.Shared.Factories;

/// <summary>
/// Reads the client-visible messages and checks them against the error definitions (ER-011, R9).
/// </summary>
public static class ErrorMessageCatalogFactory
{
    /// <exception cref="InvalidOperationException">
    /// The messages are not a valid JSON object; or a key is duplicated, is not a registered code (compared
    /// ordinally), or belongs to a server failure; or a message is not an object with a non-blank string
    /// <c>title</c> and <c>detail</c>; or a client-visible definition has no message. The message names the
    /// offending code where there is one, and otherwise the problem.
    /// </exception>
    public static ErrorMessageCatalog Create(ErrorMessageSource source, ApplicationErrorDefinitionRegistry registry)
    {
        var definitions = registry.Definitions.ToDictionary(definition => definition.Code, StringComparer.Ordinal);
        using var document = Parse(source);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("The error message file must hold one JSON object.");
        }

        var messages = new Dictionary<string, ErrorMessage>(StringComparer.Ordinal);
        foreach (var entry in document.RootElement.EnumerateObject())
        {
            var code = entry.Name;
            if (messages.ContainsKey(code))
            {
                throw new InvalidOperationException($"The error message file holds '{code}' more than once.");
            }

            if (!definitions.TryGetValue(code, out var definition))
            {
                throw new InvalidOperationException(
                    $"The error message file holds a message for '{code}', which is an unknown code.");
            }

            if (!definition.IsClientVisible)
            {
                throw new InvalidOperationException(
                    $"The error message file holds a message for '{code}', which is a server failure and is masked.");
            }

            messages.Add(code, ReadMessage(code, entry.Value));
        }

        var missing = registry.Definitions.FirstOrDefault(
            definition => definition.IsClientVisible && !messages.ContainsKey(definition.Code));
        if (missing is not null)
        {
            throw new InvalidOperationException(
                $"The client-visible error '{missing.Code}' has no message in the error message file.");
        }

        return new ErrorMessageCatalog(messages);
    }

    private static JsonDocument Parse(ErrorMessageSource source)
    {
        try
        {
            return JsonDocument.Parse(source.Content);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The error message file is not valid JSON.", exception);
        }
    }

    private static ErrorMessage ReadMessage(string code, JsonElement message)
    {
        if (message.ValueKind == JsonValueKind.Null)
        {
            throw new InvalidOperationException($"The message for '{code}' is null.");
        }

        if (message.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"The message for '{code}' must be an object with a title and a detail.");
        }

        return new ErrorMessage(ReadText(code, message, "title"), ReadText(code, message, "detail"));
    }

    private static string ReadText(string code, JsonElement message, string name)
    {
        if (!message.TryGetProperty(name, out var member) || member.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"The message for '{code}' has no {name} string.");
        }

        var text = member.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"The message for '{code}' has a blank {name}.");
        }

        return text;
    }
}
