namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// The failure of staff access control (brief 002). It is logged only, never shown to a client.
/// </summary>
[ErrorDefinitions]
public static class AccessErrorDefinitions
{
    /// <summary>The signing keys of Cloudflare Access could not be fetched.</summary>
    public static readonly ApplicationErrorDefinition KeysUnavailable =
        new("access.keys_unavailable", ApplicationErrorCategory.DependencyUnavailable);
}
