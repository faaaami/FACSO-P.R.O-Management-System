using System.Text.RegularExpressions;

namespace PROManagementSystem.Application.Common.Validation;

/// <summary>
/// Emirates ID number rules: 15 digits written 784-YYYY-NNNNNNN-N, where 784
/// is the UAE country code and YYYY the holder's birth year.
///
/// A wrong format is an error. A birth-year mismatch and a failed Luhn check
/// digit are warnings only: the Luhn rule is widely documented but not yet
/// verified against a sample of real cards, so it must not block a save.
/// </summary>
public static partial class EmiratesIdNumber
{
    public const string FormatMessage =
        "Emirates ID number must be in the format 784-YYYY-NNNNNNN-N.";

    [GeneratedRegex(@"784[\s\-]?\d{4}[\s\-]?\d{7}[\s\-]?\d")]
    private static partial Regex Candidate();

    /// <summary>
    /// Returns the canonical 784-YYYY-NNNNNNN-N form, or null when the value
    /// is not 15 digits starting with 784 (dashes and spaces are ignored).
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        var hasOnlySeparators = value.All(c => char.IsAsciiDigit(c) || c is '-' or ' ');

        if (!hasOnlySeparators || digits.Length != 15 || !digits.StartsWith("784", StringComparison.Ordinal))
        {
            return null;
        }

        return $"{digits[..3]}-{digits[3..7]}-{digits[7..14]}-{digits[14]}";
    }

    /// <summary>Finds the first Emirates ID number in free (OCR) text.</summary>
    public static string? FindInText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = Candidate().Match(text);
        return match.Success ? Normalize(match.Value) : null;
    }

    public static bool IsValidFormat(string? value) => Normalize(value) is not null;

    /// <summary>Warnings for a correctly formatted number.</summary>
    public static IReadOnlyList<string> Warnings(string? value, DateOnly? dateOfBirth)
    {
        var normalized = Normalize(value);

        if (normalized is null)
        {
            return Array.Empty<string>();
        }

        var warnings = new List<string>();
        var digits = normalized.Replace("-", string.Empty);

        if (dateOfBirth is { } dob &&
            int.Parse(digits[3..7]) != dob.Year)
        {
            warnings.Add(
                "The year in the Emirates ID number does not match the date of birth. " +
                "Check both against the card.");
        }

        if (!LuhnValid(digits))
        {
            warnings.Add(
                "The last digit of the Emirates ID number looks wrong. " +
                "Check the number against the card.");
        }

        return warnings;
    }

    public static bool LuhnValid(string digits)
    {
        var sum = 0;
        var doubleIt = false;

        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';

            if (doubleIt)
            {
                n *= 2;
                if (n > 9)
                {
                    n -= 9;
                }
            }

            sum += n;
            doubleIt = !doubleIt;
        }

        return sum % 10 == 0;
    }
}
