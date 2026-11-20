using Minioasis.Application.Shared.Errors;
using System.Diagnostics.CodeAnalysis;

namespace Minioasis.Application.Shared.Results;

/// <summary>
/// The result of an Application operation: success with its response, or failure with an error (AR-012).
/// </summary>
public sealed class ApplicationResult<TResponse>
{
    private ApplicationResult(TResponse? response, ApplicationError? error)
    {
        Response = response;
        Error = error;
    }

    [MemberNotNullWhen(true, nameof(Response))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public TResponse? Response
    {
        get;
    }

    public ApplicationError? Error
    {
        get;
    }

    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    public static ApplicationResult<TResponse> Success(TResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new ApplicationResult<TResponse>(response, error: null);
    }

    /// <exception cref="ArgumentNullException"><paramref name="error"/> is null.</exception>
    public static ApplicationResult<TResponse> Failure(ApplicationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new ApplicationResult<TResponse>(default, error);
    }
}
