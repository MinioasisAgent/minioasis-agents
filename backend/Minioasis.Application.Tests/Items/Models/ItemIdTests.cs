using Minioasis.Application.Items.Models;
using System.Globalization;
using Xunit;

namespace Minioasis.Application.Tests.Items.Models;

public sealed class ItemIdTests
{
    [Theory]
    [InlineData("1", 1)]
    [InlineData("12", 12)]
    [InlineData("1000", 1000)]
    [InlineData("2147483647", 2147483647)]
    public void Accepts_a_positive_whole_number_without_a_leading_zero(string text, int expected)
    {
        Assert.True(ItemId.TryCreate(text, out var itemId));
        Assert.Equal(expected, itemId.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" 12")]
    [InlineData("12 ")]
    [InlineData("\t12")]
    [InlineData("007")]
    [InlineData("0")]
    [InlineData("00")]
    [InlineData("abc")]
    [InlineData("12a")]
    [InlineData("1.5")]
    [InlineData("1,000")]
    [InlineData("1e3")]
    [InlineData("+5")]
    [InlineData("-1")]
    [InlineData("2147483648")]
    [InlineData("99999999999999999999")]
    [InlineData("１２")]
    [InlineData("١٢")]
    public void Refuses_anything_else(string? text)
    {
        Assert.False(ItemId.TryCreate(text, out var itemId));
        Assert.Null(itemId);
    }

    [Fact]
    public void Reads_the_number_the_same_way_when_the_culture_is_not_English()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = NonEnglishCulture();

            Assert.True(ItemId.TryCreate("1234567", out var itemId));
            Assert.Equal(1234567, itemId.Value);
            Assert.False(ItemId.TryCreate("1.234", out _));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static CultureInfo NonEnglishCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberGroupSeparator = ".";
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.PositiveSign = "1";
        culture.NumberFormat.NegativeSign = "~";
        return culture;
    }
}
