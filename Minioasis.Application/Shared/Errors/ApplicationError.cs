namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// One occurrence of an error: its definition, the values for its message's placeholders, and, for a dependency
/// failure, the dependency's status. The status is kept apart from the placeholder values, so that it is only
/// logged and never shown to a client.
/// </summary>
public sealed class ApplicationError
{
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    /// <exception cref="ArgumentException">A placeholder value is null.</exception>
    public ApplicationError(
        ApplicationErrorDefinition definition,
        IReadOnlyDictionary<string, string>? placeholderValues = null,
        int? dependencyStatus = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in placeholderValues ?? new Dictionary<string, string>())
        {
            if (value is null)
            {
                throw new ArgumentException(
                    $"The value for placeholder '{name}' is null.",
                    nameof(placeholderValues));
            }

            values.Add(name, value);
        }

        Definition = definition;
        PlaceholderValues = values.AsReadOnly();
        DependencyStatus = dependencyStatus;
    }

    public ApplicationErrorDefinition Definition
    {
        get;
    }

    public IReadOnlyDictionary<string, string> PlaceholderValues
    {
        get;
    }

    /// <summary>
    /// The dependency's HTTP status, when it replied; null otherwise. Logged with the error, never shown.
    /// </summary>
    public int? DependencyStatus
    {
        get;
    }
}
