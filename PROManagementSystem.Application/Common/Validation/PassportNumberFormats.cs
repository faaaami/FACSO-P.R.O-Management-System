namespace PROManagementSystem.Application.Common.Validation;

/// <summary>
/// Known passport-number layouts by issuing state (ICAO alpha-3), used for
/// warnings only: a mismatch never blocks a save. Each position of a mask is
/// 'A' (letter) or '9' (digit).
///
/// Only layouts we are confident about are listed. Countries with several
/// passport series in circulation (Philippines, Bangladesh) or without a
/// verified sample (Sri Lanka, Nepal, Egypt) are left out until checked
/// against real documents.
/// </summary>
public static class PassportNumberFormats
{
    private static readonly Dictionary<string, (string Mask, string Description)> Formats =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["IND"] = ("A9999999", "Indian passport numbers are 1 letter followed by 7 digits"),
            ["PAK"] = ("AA9999999", "Pakistani passport numbers are 2 letters followed by 7 digits"),
            ["GBR"] = ("999999999", "UK passport numbers are 9 digits")
        };

    // Look-alikes OCR confuses, which ICAO check digits often cannot catch.
    private static readonly Dictionary<char, string> LetterLookAlikes = new()
    {
        ['0'] = "O, D or Q", ['1'] = "I or L", ['2'] = "Z", ['5'] = "S", ['6'] = "G", ['8'] = "B"
    };

    private static readonly Dictionary<char, string> DigitLookAlikes = new()
    {
        ['O'] = "0", ['D'] = "0", ['Q'] = "0", ['I'] = "1", ['L'] = "1",
        ['Z'] = "2", ['S'] = "5", ['G'] = "6", ['B'] = "8"
    };

    public static IReadOnlyCollection<string> IssuingStates => Formats.Keys;

    /// <summary>
    /// Warnings for <paramref name="number"/> against the layout of
    /// <paramref name="issuingState"/>; empty when the state is not listed or
    /// the number matches.
    /// </summary>
    public static IReadOnlyList<string> Warnings(string? number, string? issuingState)
    {
        if (string.IsNullOrWhiteSpace(number) ||
            string.IsNullOrWhiteSpace(issuingState) ||
            !Formats.TryGetValue(issuingState.Trim(), out var format))
        {
            return Array.Empty<string>();
        }

        var value = number.Trim().ToUpperInvariant();
        var mask = format.Mask;

        if (value.Length != mask.Length)
        {
            return new[] { $"{format.Description}; this number has {value.Length} characters. Check it against the passport." };
        }

        var warnings = new List<string>();

        for (var i = 0; i < mask.Length; i++)
        {
            var c = value[i];

            if (mask[i] == 'A' && !char.IsAsciiLetterUpper(c))
            {
                var hint = LetterLookAlikes.TryGetValue(c, out var letters)
                    ? $" ('{c}' is often a misread {letters})"
                    : string.Empty;

                warnings.Add($"Character {i + 1} should be a letter but is '{c}'{hint}. {format.Description}.");
            }
            else if (mask[i] == '9' && !char.IsAsciiDigit(c))
            {
                var hint = DigitLookAlikes.TryGetValue(c, out var digit)
                    ? $" ('{c}' is often a misread {digit})"
                    : string.Empty;

                warnings.Add($"Character {i + 1} should be a digit but is '{c}'{hint}. {format.Description}.");
            }
        }

        return warnings;
    }
}
