namespace Minioasis.Application.Shared.Errors;

/// <summary>
/// Errors for a request that reaches no endpoint. They belong to no single feature.
/// </summary>
public static class RoutingErrorDefinitions
{
    public static readonly ApplicationErrorDefinition NotFound =
        new("not_found", ApplicationErrorCategory.NotFound);

    public static readonly ApplicationErrorDefinition MethodNotAllowed =
        new("method_not_allowed", ApplicationErrorCategory.MethodNotAllowed);
}
