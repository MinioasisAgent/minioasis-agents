using System.Reflection;

namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// The definitions that the registry checks. By default, every definition declared in the Application assembly;
/// a test host may register another source.
/// </summary>
public sealed class ApplicationErrorDefinitionSource
{
    /// <param name="definitions">The definitions, unchecked; a null entry is reported by the registry.</param>
    public ApplicationErrorDefinitionSource(IEnumerable<ApplicationErrorDefinition?> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        Definitions = [.. definitions];
    }

    public IReadOnlyList<ApplicationErrorDefinition?> Definitions
    {
        get;
    }

    /// <summary>
    /// Collects the public static <see cref="ApplicationErrorDefinition"/> fields of every class in
    /// <paramref name="assembly"/> marked with <see cref="ErrorDefinitionsAttribute"/>, including null ones.
    /// </summary>
    public static ApplicationErrorDefinitionSource FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var definitions = assembly.GetTypes()
            .Where(type => type.IsDefined(typeof(ErrorDefinitionsAttribute), inherit: false))
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.FieldType == typeof(ApplicationErrorDefinition))
            .Select(field => (ApplicationErrorDefinition?)field.GetValue(null));

        return new ApplicationErrorDefinitionSource(definitions);
    }
}
