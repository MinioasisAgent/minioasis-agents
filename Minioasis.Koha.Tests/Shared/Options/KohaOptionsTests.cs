using Minioasis.Koha.Shared.Options;
using Xunit;

namespace Minioasis.Koha.Tests.Shared.Options;

public sealed class KohaOptionsTests
{
    // R13, ER-008: the options are never written out with their values, for example by a record's generated ToString.
    [Fact]
    public void Text_of_the_options_contains_no_value()
    {
        var options = new KohaOptions
        {
            BaseUrl = new Uri("https://marker-host.test/api/v1/"),
            ClientId = "marker-client-id",
            ClientSecret = "marker-client-secret",
            Deadline = TimeSpan.FromSeconds(17),
        };

        var text = options.ToString();

        Assert.DoesNotContain("marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("17", text, StringComparison.Ordinal);
    }
}
