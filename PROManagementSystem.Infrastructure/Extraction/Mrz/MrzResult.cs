namespace PROManagementSystem.Infrastructure.Extraction.Mrz;

public enum MrzFormat
{
    /// <summary>ID-1 cards (3 lines × 30), e.g. the back of an Emirates ID.</summary>
    Td1,

    /// <summary>Passport books (2 lines × 44).</summary>
    Td3
}

/// <summary>
/// A field read from the MRZ. <see cref="CheckDigitValid"/> is null for
/// fields that have no check digit of their own.
/// </summary>
public sealed record MrzField(string? Value, bool? CheckDigitValid = null)
{
    public bool HasValue => !string.IsNullOrEmpty(Value);
}

/// <summary>
/// Parsed machine-readable zone. Dates are ISO yyyy-MM-dd strings (or null
/// when the digits do not form a real date). Never log instances of this
/// type: every property is personal data.
/// </summary>
public sealed record MrzResult(
    MrzFormat Format,
    string DocumentCode,
    string IssuingState,
    string Surname,
    string GivenNames,
    MrzField DocumentNumber,
    string Nationality,
    MrzField DateOfBirth,
    string Sex,
    MrzField ExpiryDate,
    string OptionalData1,
    string OptionalData2,
    bool CompositeCheckValid)
{
    public bool AllChecksValid =>
        DocumentNumber.CheckDigitValid != false &&
        DateOfBirth.CheckDigitValid != false &&
        ExpiryDate.CheckDigitValid != false &&
        CompositeCheckValid;

    public string FullName =>
        string.Join(' ', new[] { GivenNames, Surname }.Where(x => x.Length > 0));
}
