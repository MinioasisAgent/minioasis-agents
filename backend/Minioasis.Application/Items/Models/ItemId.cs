using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Minioasis.Application.Items.Models;

/// <summary>
/// A Koha item number: a whole number from 1 to 2147483647.
/// </summary>
public sealed record ItemId
{
    private ItemId(int value)
    {
        Value = value;
    }

    public int Value
    {
        get;
    }

    /// <summary>
    /// Accepts one or more ASCII digits without a leading zero, whose value fits in a 32-bit signed integer.
    /// Whitespace, a sign, a decimal point, or any other character is refused.
    /// </summary>
    public static bool TryCreate(string? text, [NotNullWhen(true)] out ItemId? itemId)
    {
        itemId = IsDigitsWithoutLeadingZero(text)
            && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                ? new ItemId(value)
                : null;
        return itemId is not null;
    }

    private static bool IsDigitsWithoutLeadingZero([NotNullWhen(true)] string? text) =>
        !string.IsNullOrEmpty(text) && text[0] != '0' && text.All(char.IsAsciiDigit);
}
