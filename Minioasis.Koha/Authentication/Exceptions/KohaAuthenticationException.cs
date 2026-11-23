namespace Minioasis.Koha.Authentication.Exceptions;

/// <summary>
/// Minioasis could not obtain a Koha access token, for any reason other than the caller cancelling (EX-013).
/// </summary>
internal sealed class KohaAuthenticationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
