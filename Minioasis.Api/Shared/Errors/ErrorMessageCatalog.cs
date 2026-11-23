using Minioasis.Application.Shared.Errors;
using System.Text.Json;

namespace Minioasis.Api.Shared.Errors;

/// <summary>
/// The messages of <c>Resources/errors.en.json</c>, checked against the error registry when the application
/// starts (ER-011). It holds exactly one message for each client-visible definition, and no other.
/// </summary>
internal sealed class ErrorMessageCatalog
{
    private const string ResourceName = "Minioasis.Api.Resources.errors.en.json";

    private readonly IReadOnlyDictionary<string, ErrorMessage> messages;

    private ErrorMessageCatalog(IReadOnlyDictionary<string, ErrorMessage> messages)
    {
        this.messages = messages;
    }

    /// <summary>
    /// Reads the messages and checks them against the registry.
    /// </summary>
    /// <exception cref="InvalidOperationException">The registry or the messages are inconsistent.</exception>
    public static ErrorMessageCatalog Load(ApplicationErrorRegistry registry, string json)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var problems = new List<string>();
        var definitions = CheckRegistry(registry, problems);
        var messages = ReadMessages(json, problems);
        CheckCoverage(definitions, messages, problems);

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "The error registry and Resources/errors.en.json are inconsistent: " + string.Join(" ", problems));
        }

        return new ErrorMessageCatalog(messages);
    }

    /// <summary>Reads <c>Resources/errors.en.json</c>, which the build embeds in the assembly.</summary>
    public static string ReadEmbeddedMessages()
    {
        using var stream = typeof(ErrorMessageCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource {ResourceName} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>The message of a client-visible error.</summary>
    public ErrorMessage Get(string code) =>
        messages.TryGetValue(code, out var message)
            ? message
            : throw new InvalidOperationException($"No message exists for the error code {code}.");

    private static Dictionary<string, ApplicationErrorDefinition> CheckRegistry(
        ApplicationErrorRegistry registry,
        List<string> problems)
    {
        var definitions = new Dictionary<string, ApplicationErrorDefinition>(StringComparer.Ordinal);
        if (registry.Definitions.Count == 0)
        {
            problems.Add("The registry is empty.");
        }

        foreach (var definition in registry.Definitions)
        {
            if (definition is null)
            {
                problems.Add("The registry holds a null definition.");
            }
            else if (string.IsNullOrWhiteSpace(definition.Code))
            {
                problems.Add("The registry holds a definition with a blank code.");
            }
            else if (!Enum.IsDefined(definition.Category))
            {
                problems.Add($"The definition {definition.Code} has an invalid category.");
            }
            else if (!definitions.TryAdd(definition.Code, definition))
            {
                problems.Add($"The code {definition.Code} is defined more than once.");
            }
        }

        return definitions;
    }

    private static Dictionary<string, ErrorMessage> ReadMessages(string json, List<string> problems)
    {
        var messages = new Dictionary<string, ErrorMessage>(StringComparer.Ordinal);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            problems.Add("The message file is not valid JSON.");
            return messages;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                problems.Add("The message file is not a JSON object.");
                return messages;
            }

            foreach (var entry in document.RootElement.EnumerateObject())
            {
                var message = ReadMessage(entry, problems);
                if (message is not null && !messages.TryAdd(entry.Name, message))
                {
                    problems.Add($"The message {entry.Name} appears more than once.");
                }
            }
        }

        return messages;
    }

    private static ErrorMessage? ReadMessage(JsonProperty entry, List<string> problems)
    {
        if (entry.Value.ValueKind == JsonValueKind.Null)
        {
            problems.Add($"The message {entry.Name} is null.");
            return null;
        }

        if (entry.Value.ValueKind != JsonValueKind.Object
            || entry.Value.EnumerateObject().Any(field => field.Name is not ("title" or "detail")))
        {
            problems.Add($"The message {entry.Name} is malformed: it must be an object with only a title and a detail.");
            return null;
        }

        var title = ReadText(entry.Value, "title");
        var detail = ReadText(entry.Value, "detail");
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(detail))
        {
            problems.Add($"The message {entry.Name} is incomplete: it needs a title and a detail that are not blank.");
            return null;
        }

        return new ErrorMessage(title, detail);
    }

    private static string? ReadText(JsonElement message, string field) =>
        message.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static void CheckCoverage(
        Dictionary<string, ApplicationErrorDefinition> definitions,
        Dictionary<string, ErrorMessage> messages,
        List<string> problems)
    {
        foreach (var definition in definitions.Values.Where(definition => definition.IsClientVisible))
        {
            if (!messages.ContainsKey(definition.Code))
            {
                problems.Add($"The client-visible error {definition.Code} has no message.");
            }
        }

        foreach (var code in messages.Keys)
        {
            if (!definitions.TryGetValue(code, out var definition))
            {
                problems.Add($"The message {code} belongs to no registered definition.");
            }
            else if (!definition.IsClientVisible)
            {
                problems.Add($"The message {code} belongs to a server failure, which is never shown to a client.");
            }
        }
    }
}
