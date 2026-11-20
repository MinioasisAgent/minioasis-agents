using Xunit;

namespace Minioasis.Api.Tests.Shared.Factories;

// The guard is tested over a list of names, so no test changes the real process environment (TS-009).
public sealed class ApiTestHostFactoryTests
{
    [Theory]
    [InlineData("MINIOASIS_KOHA_BASE_URL")]
    [InlineData("MINIOASIS_KOHA_CLIENT_ID")]
    [InlineData("MINIOASIS_KOHA_CLIENT_SECRET")]
    [InlineData("MINIOASIS_KOHA_TIMEOUT_SECONDS")]
    [InlineData("MINIOASIS_KOHA_ANYTHING_ELSE")]
    [InlineData("minioasis_koha_client_secret")]
    [InlineData("ASPNETCORE_MINIOASIS_KOHA_BASE_URL")]
    [InlineData("DOTNET_MINIOASIS_KOHA_CLIENT_SECRET")]
    public void Koha_variable_in_the_environment_is_refused(string name)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ApiTestHostFactory.EnsureNoKohaVariables(["PATH", name, "HOME"]));

        Assert.Contains(name, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("PATH", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_koha_variable_set_is_named()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ApiTestHostFactory.EnsureNoKohaVariables(["MINIOASIS_KOHA_BASE_URL", "MINIOASIS_KOHA_CLIENT_SECRET"]));

        Assert.Contains("MINIOASIS_KOHA_BASE_URL", exception.Message, StringComparison.Ordinal);
        Assert.Contains("MINIOASIS_KOHA_CLIENT_SECRET", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData]
    [InlineData("PATH", "HOME")]
    [InlineData("MINIOASIS_OTHER", "KOHA_BASE_URL", "MINIOASIS_KOHABASE_URL", "X_MINIOASIS_KOHA_BASE_URL")]
    public void Other_variables_are_accepted(params string[] names)
    {
        ApiTestHostFactory.EnsureNoKohaVariables(names);
    }
}
