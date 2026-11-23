using Minioasis.Application.Items.Errors;

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
    } = new(
    [
        ItemErrorDefinitions.InvalidIdentifier,
        ItemErrorDefinitions.NotFound,
        KohaErrorDefinitions.AuthenticationFailed,
        KohaErrorDefinitions.InvalidResponse,
        KohaErrorDefinitions.Unavailable,
        SystemErrorDefinitions.Unexpected,
    ]);

    /// <summary>
    /// The definitions as they were registered. Null entries are kept, so that the startup check can reject them.
    /// </summary>
    public IReadOnlyList<ApplicationErrorDefinition?> Definitions { get; } = definitions;
}
