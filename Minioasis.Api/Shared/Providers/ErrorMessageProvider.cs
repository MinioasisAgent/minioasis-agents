using Minioasis.Api.Shared.Models;
using Minioasis.Application.Shared.Errors;
using System.Text.RegularExpressions;

namespace Minioasis.Api.Shared.Providers;

/// <summary>
/// The English title and detail of a client-visible error, with each <c>{name}</c> placeholder replaced by the
/// value the error supplied (R2).
/// </summary>
public sealed partial class ErrorMessageProvider(ErrorMessageCatalog catalog)
{
    /// <exception cref="InvalidOperationException">
    /// The error's code has no message, a placeholder has no value, or a value is supplied for a name that is not
    /// a placeholder of the message. Each is a programming error, answered as an unhandled exception (R2).
    /// </exception>
    public ErrorMessage GetMessage(ApplicationError error)
    {
        var code = error.Definition.Code;
        if (!catalog.TryGetMessage(code, out var message))
        {
            throw new InvalidOperationException($"The error '{code}' has no client-visible message.");
        }

        var placeholders = Placeholder().Matches(message.Title)
            .Concat(Placeholder().Matches(message.Detail))
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);
        var missing = placeholders.FirstOrDefault(name => !error.PlaceholderValues.ContainsKey(name));
        if (missing is not null)
        {
            throw new InvalidOperationException($"The error '{code}' has no value for the placeholder '{missing}'.");
        }

        var extra = error.PlaceholderValues.Keys.FirstOrDefault(name => !placeholders.Contains(name));
        if (extra is not null)
        {
            throw new InvalidOperationException(
                $"The error '{code}' has a value for '{extra}', which is not a placeholder.");
        }

        return new ErrorMessage(Fill(message.Title, error), Fill(message.Detail, error));
    }

    /// <summary>One pass, so that a value that looks like a placeholder is inserted as it is.</summary>
    private static string Fill(string template, ApplicationError error) =>
        Placeholder().Replace(template, match => error.PlaceholderValues[match.Groups["name"].Value]);

    [GeneratedRegex(@"\{(?<name>[A-Za-z0-9_]+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}
