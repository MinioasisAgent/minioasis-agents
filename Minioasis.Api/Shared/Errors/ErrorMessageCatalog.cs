using Minioasis.Application.Shared.Errors;
using System.Text.Json;

namespace Minioasis.Api.Shared.Errors;

/// <summary>
/// The messages of <c>Resources/errors.en.json</c>, checked against the error registry and Api's errors of HTTP
/// itself when the application starts (ER-011). It holds exactly one message for each client-visible definition of
/// either, and no other.
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
    /// Reads the messages and checks them against the registry and the errors of HTTP itself.
    /// </summary>
    /// <exception cref="InvalidOperationException">The definitions or the messages are inconsistent.</exception>
    public static ErrorMessageCatalog Load(
        ApplicationErrorRegistry registry,
        IReadOnlyList<HttpErrorDefinition?> httpErrors,
        string json)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(httpErrors);

        var problems = new List<string>();
        var clientVisibleByCode = new Dictionary<string, bool>(StringComparer.Ordinal);
        CheckRegistry(registry, clientVisibleByCode, problems);
        CheckHttpErrors(httpErrors, clientVisibleByCode, problems);
        var messages = ReadMessages(json, problems);
        CheckCoverage(clientVisibleByCode, messages, problems);

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "The error definitions and Resources/errors.en.json are inconsistent: " + string.Join(" ", problems));
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

    private static void CheckRegistry(
        ApplicationErrorRegistry registry,
        Dictionary<string, bool> clientVisibleByCode,
        List<string> problems)
    {
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
            else if (!clientVisibleByCode.TryAdd(definition.Code, definition.IsClientVisible))
            {
                problems.Add($"The code {definition.Code} is defined more than once.");
            }
        }
    }

    /// <summary>
    /// Every error of HTTP itself is client-visible, so its status must be a client error (4xx); a server failure is
    /// masked instead (ER-006). Its code must not repeat a code of the registry.
    /// </summary>
    private static void CheckHttpErrors(
        IReadOnlyList<HttpErrorDefinition?> httpErrors,
        Dictionary<string, bool> clientVisibleByCode,
        List<string> problems)
    {
        foreach (var error in httpErrors)
        {
            if (error is null)
            {
                problems.Add("The errors of HTTP itself hold a null definition.");
            }
            else if (string.IsNullOrWhiteSpace(error.Code))
            {
                problems.Add("The errors of HTTP itself hold a definition with a blank code.");
            }
            else if (error.Status is < StatusCodes.Status400BadRequest or > 499)
            {
                problems.Add($"The error of HTTP itself {error.Code} has the status {error.Status}, which is not a client error.");
            }
            else if (!clientVisibleByCode.TryAdd(error.Code, true))
            {
                problems.Add($"The code {error.Code} is defined more than once.");
            }
        }
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
        Dictionary<string, bool> clientVisibleByCode,
        Dictionary<string, ErrorMessage> messages,
        List<string> problems)
    {
        foreach (var code in clientVisibleByCode.Where(pair => pair.Value).Select(pair => pair.Key))
        {
            if (!messages.ContainsKey(code))
            {
                problems.Add($"The client-visible error {code} has no message.");
            }
        }

        foreach (var code in messages.Keys)
        {
            if (!clientVisibleByCode.TryGetValue(code, out var clientVisible))
            {
                problems.Add($"The message {code} belongs to no defined error.");
            }
            else if (!clientVisible)
            {
                problems.Add($"The message {code} belongs to a server failure, which is never shown to a client.");
            }
        }
    }
}
