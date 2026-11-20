using System.Diagnostics.CodeAnalysis;

namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// Every error definition of the application, checked when it is created (ER-011, R9).
/// </summary>
public sealed class ApplicationErrorDefinitionRegistry
{
    /// <exception cref="InvalidOperationException">
    /// The source is empty, or holds a null definition, a blank or duplicate code, or an undefined category.
    /// The message names the offending code where there is one, and otherwise the problem.
    /// </exception>
    public ApplicationErrorDefinitionRegistry(ApplicationErrorDefinitionSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Definitions.Count == 0)
        {
            throw new InvalidOperationException("The error definition registry is empty.");
        }

        var codes = new HashSet<string>(StringComparer.Ordinal);
        var definitions = new List<ApplicationErrorDefinition>(source.Definitions.Count);
        foreach (var definition in source.Definitions)
        {
            Check(definition, codes);
            definitions.Add(definition);
        }

        Definitions = definitions.AsReadOnly();
    }

    public IReadOnlyList<ApplicationErrorDefinition> Definitions
    {
        get;
    }

    private static void Check(
        [NotNull] ApplicationErrorDefinition? definition,
        HashSet<string> codes)
    {
        if (definition is null)
        {
            throw new InvalidOperationException("The error definition registry holds a null definition.");
        }

        if (string.IsNullOrWhiteSpace(definition.Code))
        {
            throw new InvalidOperationException("The error definition registry holds a definition with a blank code.");
        }

        if (!Enum.IsDefined(definition.Category))
        {
            throw new InvalidOperationException(
                $"Error definition '{definition.Code}' has the undefined category {(int)definition.Category}.");
        }

        if (!codes.Add(definition.Code))
        {
            throw new InvalidOperationException($"Error definition '{definition.Code}' is defined more than once.");
        }
    }
}
