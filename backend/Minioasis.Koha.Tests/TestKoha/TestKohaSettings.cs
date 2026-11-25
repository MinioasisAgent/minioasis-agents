namespace Minioasis.Koha.Tests.TestKoha;

/// <summary>
/// The five MINIOASIS_TEST_KOHA_* variables (TS-013). Their values are never printed or written anywhere.
/// </summary>
internal sealed record TestKohaSettings(
    string BaseUrl,
    string ClientId,
    string ClientSecret,
    string CloudflareClientId,
    string CloudflareClientSecret)
{
    public const string SkipReason =
        "test Koha: not run, because one of the five MINIOASIS_TEST_KOHA_* variables is not set.";

    /// <summary>The settings, or null when any variable is missing or blank.</summary>
    public static TestKohaSettings? FromEnvironment()
    {
        var values = new[] { "BASE_URL", "CLIENT_ID", "CLIENT_SECRET", "CF_ACCESS_CLIENT_ID", "CF_ACCESS_CLIENT_SECRET" }
            .Select(name => Environment.GetEnvironmentVariable($"MINIOASIS_TEST_KOHA_{name}"))
            .ToArray();

        return values.Any(string.IsNullOrWhiteSpace)
            ? null
            : new TestKohaSettings(values[0]!, values[1]!, values[2]!, values[3]!, values[4]!);
    }

    /// <summary>Hides the values if a test framework prints the record.</summary>
    public override string ToString() => nameof(TestKohaSettings);
}
