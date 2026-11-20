namespace Minioasis.Api.Shared.Abstractions;

/// <summary>
/// A group of endpoints. Api maps every module registered in dependency injection.
/// </summary>
public interface IEndpointModule
{
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
