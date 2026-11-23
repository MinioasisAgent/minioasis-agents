namespace Minioasis.Application.Items.Outcomes;

/// <summary>
/// What can happen when the gateway is asked for an item.
/// </summary>
public enum GetItemOutcome
{
    Found = 1,
    NotFound,
    AuthenticationFailed,
    InvalidResponse,
    Unavailable,
}
