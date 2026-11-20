namespace Minioasis.Koha.Shared.Exceptions;

/// <summary>
/// A Koha setting is missing or invalid, so the application must not start (spec 001, R10 to R12). The message names
/// the variable and never contains its value or any other configured value (R13).
/// </summary>
public sealed class KohaConfigurationException(string message) : Exception(message);
