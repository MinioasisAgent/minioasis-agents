using Minioasis.Application.Shared.Errors;
using System.Diagnostics.CodeAnalysis;

namespace Minioasis.Application.Shared.Results;

/// <summary>
/// The success or failure of a use case: its response, or its error.
/// </summary>
public sealed class ApplicationResult<TResponse>
    where TResponse : class
{
    private ApplicationResult(TResponse? value, ApplicationError? error)
    {
        Value = value;
        Error = error;
    }

    public TResponse? Value
    {
        get;
    }

    public ApplicationError? Error
    {
        get;
    }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static ApplicationResult<TResponse> Success(TResponse value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ApplicationResult<TResponse>(value, null);
    }

    public static ApplicationResult<TResponse> Failure(ApplicationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ApplicationResult<TResponse>(null, error);
    }
}
