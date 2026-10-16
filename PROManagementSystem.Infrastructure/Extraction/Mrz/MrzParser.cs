using System.Globalization;

namespace PROManagementSystem.Infrastructure.Extraction.Mrz;

/// <summary>
/// Parses ICAO 9303 machine-readable zones: TD3 (passports, 2 × 44) and TD1
/// (ID cards such as the back of an Emirates ID, 3 × 30).
///
/// OCR output is tolerated: lines are normalised to A–Z, 0–9 and '&lt;';
/// characters in numeric positions are mapped to digits (O→0, I→1, …) and in
/// alphabetic positions to letters; a document number whose check digit fails
/// is retried with letter O read as zero. Every check digit is verified and
/// reported per field.
/// </summary>
public static class MrzParser
{
    public const int Td3LineLength = 44;
    public const int Td1LineLength = 30;

    private const int LengthTolerance = 2;

    /// <summary>Searches free OCR text for a TD3 or TD1 zone.</summary>
    public static MrzResult? FindAndParse(string? text, MrzFormat? expected = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var lines = text
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(NormalizeLine)
            .Where(line => line.Length >= Td1LineLength - LengthTolerance)
            .ToList();

        MrzResult? best = null;

        if (expected is null or MrzFormat.Td3)
        {
            for (var i = 0; i + 1 < lines.Count; i++)
            {
                if (IsNear(lines[i], Td3LineLength) &&
                    IsNear(lines[i + 1], Td3LineLength) &&
                    TryParseTd3(lines[i], lines[i + 1], out var result))
                {
                    best = Prefer(best, result);
                }
            }
        }

        if (expected is null or MrzFormat.Td1)
        {
            for (var i = 0; i + 2 < lines.Count; i++)
            {
                if (IsNear(lines[i], Td1LineLength) &&
                    IsNear(lines[i + 1], Td1LineLength) &&
                    IsNear(lines[i + 2], Td1LineLength) &&
                    TryParseTd1(lines[i], lines[i + 1], lines[i + 2], out var result))
                {
                    best = Prefer(best, result);
                }
            }
        }

        return best;
    }

    public static bool TryParseTd3(string line1, string line2, out MrzResult result)
    {
        result = null!;

        var l1 = Fit(NormalizeLine(line1), Td3LineLength);
        var l2 = Fit(NormalizeLine(line2), Td3LineLength);

        if (l1 is null || l2 is null || !char.IsLetter(l1[0]))
        {
            return false;
        }

        // Alphabetic positions: document code, issuing state, name,
        // nationality, sex.
        ToAlpha(l1, 0, 44);
        ToAlpha(l2, 10, 13);
        ToAlpha(l2, 20, 21);

        // Numeric positions: check digits and dates.
        ToDigits(l2, 9, 10);
        ToDigits(l2, 13, 20);
        ToDigits(l2, 21, 28);
        ToDigits(l2, 42, 44);

        var documentNumberValid = RepairDocumentNumber(l2, 0, 9, 9);

        var personalNumber = Slice(l2, 28, 42);
        var personalCheck = l2[42];
        var personalValid = personalNumber.All(c => c == '<') && personalCheck is '<' or '0'
            || MrzCheckDigit.IsValid(personalNumber, personalCheck);

        var composite = string.Concat(Slice(l2, 0, 10), Slice(l2, 13, 20), Slice(l2, 21, 43));
        var compositeValid = MrzCheckDigit.IsValid(composite, l2[43]) && personalValid;

        var (surname, givenNames) = ParseName(Slice(l1, 5, 44));

        result = new MrzResult(
            MrzFormat.Td3,
            Trim(Slice(l1, 0, 2)),
            Trim(Slice(l1, 2, 5)),
            surname,
            givenNames,
            new MrzField(Trim(Slice(l2, 0, 9)), documentNumberValid),
            Trim(Slice(l2, 10, 13)),
            DateField(Slice(l2, 13, 19), l2[19], isBirthDate: true),
            Sex(l2[20]),
            DateField(Slice(l2, 21, 27), l2[27], isBirthDate: false),
            Trim(personalNumber),
            string.Empty,
            compositeValid);

        return true;
    }

    public static bool TryParseTd1(
        string line1,
        string line2,
        string line3,
        out MrzResult result)
    {
        result = null!;

        var l1 = Fit(NormalizeLine(line1), Td1LineLength);
        var l2 = Fit(NormalizeLine(line2), Td1LineLength);
        var l3 = Fit(NormalizeLine(line3), Td1LineLength);

        if (l1 is null || l2 is null || l3 is null || !char.IsLetter(l1[0]))
        {
            return false;
        }

        ToAlpha(l1, 0, 5);
        ToAlpha(l2, 7, 8);
        ToAlpha(l2, 15, 18);
        ToAlpha(l3, 0, 30);

        ToDigits(l2, 0, 7);
        ToDigits(l2, 8, 15);
        ToDigits(l2, 29, 30);

        string documentNumber;
        bool documentNumberValid;

        if (l1[14] == '<' && l1[15] != '<')
        {
            // Long document number: it continues in optional data 1, whose
            // last character before the filler is the check digit.
            var optional = Slice(l1, 15, 30).TrimEnd('<');
            var extended = string.Concat(Slice(l1, 5, 14), optional[..^1]);
            documentNumber = extended;
            documentNumberValid = MrzCheckDigit.IsValid(extended, optional[^1]);
        }
        else
        {
            ToDigits(l1, 14, 15);
            documentNumberValid = RepairDocumentNumber(l1, 5, 14, 14);
            documentNumber = Trim(Slice(l1, 5, 14));
        }

        var composite = string.Concat(
            Slice(l1, 5, 30),
            Slice(l2, 0, 7),
            Slice(l2, 8, 15),
            Slice(l2, 18, 29));

        var (surname, givenNames) = ParseName(Slice(l3, 0, 30));

        result = new MrzResult(
            MrzFormat.Td1,
            Trim(Slice(l1, 0, 2)),
            Trim(Slice(l1, 2, 5)),
            surname,
            givenNames,
            new MrzField(documentNumber.TrimEnd('<'), documentNumberValid),
            Trim(Slice(l2, 15, 18)),
            DateField(Slice(l2, 0, 6), l2[6], isBirthDate: true),
            Sex(l2[7]),
            DateField(Slice(l2, 8, 14), l2[14], isBirthDate: false),
            Trim(Slice(l1, 15, 30)),
            Trim(Slice(l2, 18, 29)),
            MrzCheckDigit.IsValid(composite, l2[29]));

        return true;
    }

    /// <summary>Upper-cases and keeps only MRZ characters.</summary>
    public static string NormalizeLine(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        var chars = new List<char>(raw.Length);

        foreach (var c in raw.ToUpperInvariant())
        {
            switch (c)
            {
                case >= 'A' and <= 'Z':
                case >= '0' and <= '9':
                case '<':
                    chars.Add(c);
                    break;
                // Common OCR renderings of the filler character.
                case '«' or '‹' or '≤':
                    chars.Add('<');
                    break;
            }
        }

        return new string(chars.ToArray());
    }

    private static MrzResult Prefer(MrzResult? current, MrzResult candidate)
    {
        if (current is null)
        {
            return candidate;
        }

        return Score(candidate) > Score(current) ? candidate : current;

        static int Score(MrzResult r) =>
            (r.DocumentNumber.CheckDigitValid == true ? 1 : 0) +
            (r.DateOfBirth.CheckDigitValid == true ? 1 : 0) +
            (r.ExpiryDate.CheckDigitValid == true ? 1 : 0) +
            (r.CompositeCheckValid ? 2 : 0);
    }

    private static bool IsNear(string line, int length)
        => Math.Abs(line.Length - length) <= LengthTolerance;

    /// <summary>
    /// Brings a line to its exact length by padding or removing trailing
    /// fillers. Lines that would need non-filler characters removed are
    /// rejected rather than guessed.
    /// </summary>
    private static char[]? Fit(string line, int length)
    {
        if (line.Length < length)
        {
            return line.PadRight(length, '<').ToCharArray();
        }

        if (line.Length == length)
        {
            return line.ToCharArray();
        }

        var excess = line.Length - length;

        return line.EndsWith(new string('<', excess), StringComparison.Ordinal)
            ? line[..length].ToCharArray()
            : null;
    }

    private static readonly Dictionary<char, char> LetterToDigit = new()
    {
        ['O'] = '0', ['Q'] = '0', ['D'] = '0',
        ['I'] = '1', ['L'] = '1',
        ['Z'] = '2', ['S'] = '5', ['G'] = '6', ['B'] = '8'
    };

    private static readonly Dictionary<char, char> DigitToLetter = new()
    {
        ['0'] = 'O', ['1'] = 'I', ['2'] = 'Z', ['5'] = 'S',
        ['6'] = 'G', ['8'] = 'B'
    };

    private static void ToDigits(char[] line, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (LetterToDigit.TryGetValue(line[i], out var digit))
            {
                line[i] = digit;
            }
        }
    }

    private static void ToAlpha(char[] line, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (DigitToLetter.TryGetValue(line[i], out var letter))
            {
                line[i] = letter;
            }
        }
    }

    /// <summary>
    /// Validates the document number against its check digit. On failure,
    /// retries with every letter O read as the digit 0 (the most common OCR
    /// confusion) and keeps that variant only if it validates. Broader
    /// look-alike guessing is deliberately not attempted: with a single check
    /// digit, about one in ten wrong variants would validate by chance.
    /// </summary>
    private static bool RepairDocumentNumber(char[] line, int start, int end, int checkIndex)
    {
        if (MrzCheckDigit.IsValid(Slice(line, start, end), line[checkIndex]))
        {
            return true;
        }

        var candidate = Slice(line, start, end).Replace('O', '0');

        if (candidate == Slice(line, start, end) ||
            !MrzCheckDigit.IsValid(candidate, line[checkIndex]))
        {
            return false;
        }

        candidate.CopyTo(0, line, start, candidate.Length);
        return true;
    }

    private static (string Surname, string GivenNames) ParseName(string field)
    {
        var separator = field.IndexOf("<<", StringComparison.Ordinal);

        var surname = separator < 0 ? field : field[..separator];
        var given = separator < 0 ? string.Empty : field[(separator + 2)..];

        return (Words(surname), Words(given));

        static string Words(string part)
            => string.Join(' ', part.Split('<', StringSplitOptions.RemoveEmptyEntries));
    }

    private static MrzField DateField(string yymmdd, char checkDigit, bool isBirthDate)
        => new(ToIsoDate(yymmdd, isBirthDate), MrzCheckDigit.IsValid(yymmdd, checkDigit));

    private static string? ToIsoDate(string yymmdd, bool isBirthDate)
    {
        if (yymmdd.Length != 6 || !yymmdd.All(char.IsAsciiDigit))
        {
            return null;
        }

        var yy = int.Parse(yymmdd[..2], CultureInfo.InvariantCulture);
        var currentYy = DateTime.UtcNow.Year % 100;

        // Birth dates are in the past; expiry dates within this century.
        var year = isBirthDate
            ? (yy > currentYy ? 1900 + yy : 2000 + yy)
            : (yy >= 70 ? 1900 + yy : 2000 + yy);

        return DateOnly.TryParseExact(
            $"{year:0000}{yymmdd[2..]}",
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
    }

    private static string Sex(char c) => c switch
    {
        'M' => "M",
        'F' => "F",
        _ => "X"
    };

    private static string Slice(char[] line, int start, int end)
        => new(line, start, end - start);

    private static string Trim(string value) => value.Replace('<', ' ').Trim();
}
