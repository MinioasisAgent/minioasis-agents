using System.Diagnostics.CodeAnalysis;

namespace Minioasis.Api.Shared.Models;

/// <summary>
/// The checked client-visible messages, keyed by error code with ordinal comparison. Built by
/// <c>ErrorMessageCatalogFactory</c>, which checks them against the registry (R9).
/// </summary>
public sealed class ErrorMessageCatalog(IReadOnlyDictionary<string, ErrorMessage> messages)
{
    private readonly Dictionary<string, ErrorMessage> messages = new(messages, StringComparer.Ordinal);

    public bool TryGetMessage(string code, [NotNullWhen(true)] out ErrorMessage? message) =>
        messages.TryGetValue(code, out message);
}
