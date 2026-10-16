using PROManagementSystem.Infrastructure.Extraction.Mrz;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// MRZ parsing against the ICAO 9303 specimen documents (fictional holder
/// ANNA MARIA ERIKSSON, state "UTO") and a synthetic Emirates ID back side.
/// </summary>
public sealed class MrzParserTests
{
    // ICAO 9303 Part 4, TD3 specimen.
    internal const string Td3Line1 = "P<UTOERIKSSON<<ANNA<MARIA<<<<<<<<<<<<<<<<<<<";
    internal const string Td3Line2 = "L898902C36UTO7408122F1204159ZE184226B<<<<<10";

    // ICAO 9303 Part 5, TD1 specimen.
    internal const string Td1Line1 = "I<UTOD231458907<<<<<<<<<<<<<<<";
    internal const string Td1Line2 = "7408122F1204159UTO<<<<<<<<<<<6";
    internal const string Td1Line3 = "ERIKSSON<<ANNA<MARIA<<<<<<<<<<";

    // Synthetic Emirates ID back side (fake person; all check digits valid).
    internal const string EidLine1 = "ILARE1234567897784199012345676";
    internal const string EidLine2 = "9001158M3102204PAK<<<<<<<<<<<8";
    internal const string EidLine3 = "KHAN<<MOHAMMED<ALI<<<<<<<<<<<<";

    [Fact(DisplayName = "ICAO TD3 specimen parses with every check digit valid")]
    public void Td3Specimen_Parses()
    {
        Assert.True(MrzParser.TryParseTd3(Td3Line1, Td3Line2, out var mrz));

        Assert.Equal(MrzFormat.Td3, mrz.Format);
        Assert.Equal("P", mrz.DocumentCode);
        Assert.Equal("UTO", mrz.IssuingState);
        Assert.Equal("ERIKSSON", mrz.Surname);
        Assert.Equal("ANNA MARIA", mrz.GivenNames);
        Assert.Equal("L898902C3", mrz.DocumentNumber.Value);
        Assert.True(mrz.DocumentNumber.CheckDigitValid);
        Assert.Equal("UTO", mrz.Nationality);
        Assert.Equal("1974-08-12", mrz.DateOfBirth.Value);
        Assert.Equal("F", mrz.Sex);
        Assert.Equal("2012-04-15", mrz.ExpiryDate.Value);
        Assert.Equal("ZE184226B", mrz.OptionalData1);
        Assert.True(mrz.CompositeCheckValid);
        Assert.True(mrz.AllChecksValid);
    }

    [Fact(DisplayName = "ICAO TD1 specimen parses with every check digit valid")]
    public void Td1Specimen_Parses()
    {
        Assert.True(MrzParser.TryParseTd1(Td1Line1, Td1Line2, Td1Line3, out var mrz));

        Assert.Equal(MrzFormat.Td1, mrz.Format);
        Assert.Equal("D23145890", mrz.DocumentNumber.Value);
        Assert.Equal("1974-08-12", mrz.DateOfBirth.Value);
        Assert.Equal("2012-04-15", mrz.ExpiryDate.Value);
        Assert.Equal("UTO", mrz.Nationality);
        Assert.Equal("ERIKSSON", mrz.Surname);
        Assert.Equal("ANNA MARIA", mrz.GivenNames);
        Assert.True(mrz.AllChecksValid);
    }

    [Fact(DisplayName = "Emirates ID back MRZ yields card number and the 784 ID number")]
    public void EmiratesIdBack_Parses()
    {
        Assert.True(MrzParser.TryParseTd1(EidLine1, EidLine2, EidLine3, out var mrz));

        Assert.Equal("ARE", mrz.IssuingState);
        Assert.Equal("123456789", mrz.DocumentNumber.Value);
        Assert.Equal("784199012345676", mrz.OptionalData1);
        Assert.Equal("1990-01-15", mrz.DateOfBirth.Value);
        Assert.Equal("2031-02-20", mrz.ExpiryDate.Value);
        Assert.Equal("PAK", mrz.Nationality);
        Assert.Equal("M", mrz.Sex);
        Assert.Equal("MOHAMMED ALI KHAN", mrz.FullName);
        Assert.True(mrz.AllChecksValid);
    }

    [Theory(DisplayName = "A corrupted field fails exactly its own check digit")]
    [InlineData(0, "L898902C3", "L898903C3", "document")]
    [InlineData(13, "740812", "740813", "birth")]
    [InlineData(21, "120415", "120416", "expiry")]
    public void CorruptedField_FailsItsCheck(int start, string original, string corrupted, string field)
    {
        var line2 = Td3Line2[..start] + corrupted + Td3Line2[(start + original.Length)..];

        Assert.True(MrzParser.TryParseTd3(Td3Line1, line2, out var mrz));

        Assert.Equal(field == "document", mrz.DocumentNumber.CheckDigitValid == false);
        Assert.Equal(field == "birth", mrz.DateOfBirth.CheckDigitValid == false);
        Assert.Equal(field == "expiry", mrz.ExpiryDate.CheckDigitValid == false);
        Assert.False(mrz.CompositeCheckValid);
        Assert.False(mrz.AllChecksValid);
    }

    [Fact(DisplayName = "Letters OCR'd in date and check-digit positions are corrected")]
    public void OcrLookAlikes_InNumericPositions_AreCorrected()
    {
        // 7408122 → 74O8I22, check digit 9 → 9, expiry 120415 → I2O4I5
        var noisy = "L898902C36UTO74O8I22F I2O4I59ZE184226B<<<<<1O";

        Assert.True(MrzParser.TryParseTd3(Td3Line1, noisy, out var mrz));

        Assert.Equal("1974-08-12", mrz.DateOfBirth.Value);
        Assert.Equal("2012-04-15", mrz.ExpiryDate.Value);
        Assert.True(mrz.AllChecksValid);
    }

    [Fact(DisplayName = "A look-alike in the document number is repaired by its check digit")]
    public void DocumentNumberLookAlike_IsRepaired()
    {
        // OCR read the zero in L898902C3 as the letter O.
        var noisy = "L8989O2C36" + Td3Line2[10..];

        Assert.True(MrzParser.TryParseTd3(Td3Line1, noisy, out var mrz));

        Assert.Equal("L898902C3", mrz.DocumentNumber.Value);
        Assert.True(mrz.DocumentNumber.CheckDigitValid);
        Assert.True(mrz.CompositeCheckValid);
    }

    [Fact(DisplayName = "Filler characters OCR'd as « and spaces are normalised")]
    public void FillerVariants_AreNormalised()
    {
        var noisy = "P<UTOERIKSSON««ANNA<MARIA<<<<<<<<<<<<<<<<<<<";

        Assert.True(MrzParser.TryParseTd3(noisy, Td3Line2, out var mrz));

        Assert.Equal("ERIKSSON", mrz.Surname);
        Assert.Equal("ANNA MARIA", mrz.GivenNames);
    }

    [Fact(DisplayName = "The MRZ is found inside surrounding OCR text")]
    public void FindAndParse_LocatesTd3InNoise()
    {
        var text =
            "REPUBLIC OF UTOPIA\nPASSPORT No. L898902C3\n" +
            "Date of birth 12 AUG 1974\n\n" +
            Td3Line1 + "\n" + Td3Line2 + "\n";

        var mrz = MrzParser.FindAndParse(text, MrzFormat.Td3);

        Assert.NotNull(mrz);
        Assert.Equal("L898902C3", mrz!.DocumentNumber.Value);
        Assert.True(mrz.AllChecksValid);
    }

    [Fact(DisplayName = "The TD1 zone is found inside surrounding OCR text")]
    public void FindAndParse_LocatesTd1InNoise()
    {
        var text = "United Arab Emirates\nsome back side text\n" +
                   EidLine1 + "\n" + EidLine2 + "\n" + EidLine3;

        var mrz = MrzParser.FindAndParse(text, MrzFormat.Td1);

        Assert.NotNull(mrz);
        Assert.Equal("784199012345676", mrz!.OptionalData1);
    }

    [Theory(DisplayName = "Text without an MRZ returns null")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("PASSPORT\nName: Someone\nNo: 12345")]
    public void NoMrz_ReturnsNull(string? text)
        => Assert.Null(MrzParser.FindAndParse(text));

    [Fact(DisplayName = "An impossible date gives no value but keeps the check result")]
    public void InvalidCalendarDate_HasNoValue()
    {
        // 741332 is not a date; recompute nothing, so the check fails too.
        var line2 = Td3Line2[..13] + "741332" + Td3Line2[19..];

        Assert.True(MrzParser.TryParseTd3(Td3Line1, line2, out var mrz));

        Assert.Null(mrz.DateOfBirth.Value);
        Assert.False(mrz.DateOfBirth.CheckDigitValid);
    }

    [Theory(DisplayName = "Check digit computation matches ICAO examples")]
    [InlineData("L898902C3", '6')]
    [InlineData("740812", '2')]
    [InlineData("120415", '9')]
    [InlineData("<<<<<<", '0')]
    public void CheckDigit_MatchesIcao(string value, char expected)
        => Assert.Equal(expected, MrzCheckDigit.Compute(value));
}
