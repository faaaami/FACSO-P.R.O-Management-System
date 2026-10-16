namespace PROManagementSystem.Infrastructure.Extraction.Mrz;

/// <summary>
/// ICAO 9303 check digit: characters weighted 7, 3, 1 repeating, where
/// digits are their value, A–Z are 10–35 and the filler '&lt;' is 0.
/// </summary>
public static class MrzCheckDigit
{
    private static readonly int[] Weights = { 7, 3, 1 };

    public static char Compute(ReadOnlySpan<char> value)
    {
        var sum = 0;

        for (var i = 0; i < value.Length; i++)
        {
            sum += CharacterValue(value[i]) * Weights[i % 3];
        }

        return (char)('0' + sum % 10);
    }

    /// <summary>A filler check digit ('&lt;') counts as 0, per ICAO 9303.</summary>
    public static bool IsValid(ReadOnlySpan<char> value, char checkDigit)
        => Compute(value) == (checkDigit == '<' ? '0' : checkDigit);

    private static int CharacterValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'A' and <= 'Z' => c - 'A' + 10,
        _ => 0
    };
}
