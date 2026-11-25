using System.Reflection;

namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// Every error definition of the application, so that Api can check them all when it starts (ER-011).
/// </summary>
public sealed class ApplicationErrorRegistry(IReadOnlyList<ApplicationErrorDefinition?> definitions)
{
    /// <summary>The registry of every definition that the application declares.</summary>
    public static ApplicationErrorRegistry Default
    {
        get;
    } = Discover(typeof(ApplicationErrorRegistry).Assembly.DefinedTypes);

    /// <summary>
    /// The definitions as they were registered. Null entries are kept, so that the startup check can reject them.
    /// </summary>
    public IReadOnlyList<ApplicationErrorDefinition?> Definitions { get; } = definitions;

    /// <summary>
    /// Collects the public static readonly definition fields of every type marked with
    /// <see cref="ErrorDefinitionsAttribute"/>. It only collects: the startup check validates what it finds.
    /// </summary>
    internal static ApplicationErrorRegistry Discover(IEnumerable<Type> types) =>
        new(types
            .Where(type => type.IsDefined(typeof(ErrorDefinitionsAttribute), false))
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsInitOnly && field.FieldType == typeof(ApplicationErrorDefinition))
            .Select(field => (ApplicationErrorDefinition?)field.GetValue(null))
            .ToList());
}
