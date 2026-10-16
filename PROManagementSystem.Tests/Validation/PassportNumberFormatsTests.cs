using PROManagementSystem.Application.Common.Validation;
using Xunit;

namespace PROManagementSystem.Tests.Validation;

/// <summary>Passport-number layout warnings (India, Pakistan, UK). Fictional numbers.</summary>
public sealed class PassportNumberFormatsTests
{
    [Theory(DisplayName = "Numbers matching the known layout give no warning")]
    [InlineData("IND", "J8369854")]
    [InlineData("PAK", "AB1234567")]
    [InlineData("GBR", "123456789")]
    [InlineData("ind", " j8369854 ")]
    public void MatchingLayout_NoWarning(string state, string number)
        => Assert.Empty(PassportNumberFormats.Warnings(number, state));

    [Fact(DisplayName = "A digit in a letter position is flagged with its look-alike")]
    public void DigitInLetterPosition_IsHighlighted()
    {
        // A "1" where Indian numbers have their leading letter.
        var warning = Assert.Single(PassportNumberFormats.Warnings("18369854", "IND"));

        Assert.Contains("Character 1 should be a letter but is '1'", warning);
        Assert.Contains("I or L", warning);
    }

    [Fact(DisplayName = "A letter in a digit position is flagged with its look-alike")]
    public void LetterInDigitPosition_IsHighlighted()
    {
        var warning = Assert.Single(PassportNumberFormats.Warnings("AB12345O7", "PAK"));

        Assert.Contains("Character 8 should be a digit but is 'O'", warning);
        Assert.Contains("misread 0", warning);
    }

    [Fact(DisplayName = "Several wrong positions give one warning each")]
    public void SeveralMismatches_EachReported()
    {
        var warnings = PassportNumberFormats.Warnings("1234567SB", "GBR");

        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, w => w.StartsWith("Character 8 should be a digit but is 'S'"));
        Assert.Contains(warnings, w => w.StartsWith("Character 9 should be a digit but is 'B'"));
    }

    [Fact(DisplayName = "A wrong length is a single warning")]
    public void WrongLength_Warns()
    {
        var warning = Assert.Single(PassportNumberFormats.Warnings("J836985", "IND"));
        Assert.Contains("7 characters", warning);
    }

    [Theory(DisplayName = "Unlisted states or missing values give no warning")]
    [InlineData("PHL", "P1234567A")]
    [InlineData("UTO", "L898902C3")]
    [InlineData("IND", null)]
    [InlineData(null, "J8369854")]
    public void UnknownOrMissing_NoWarning(string? state, string? number)
        => Assert.Empty(PassportNumberFormats.Warnings(number, state));

    [Fact(DisplayName = "Only the confident layouts are listed")]
    public void ListedStates()
        => Assert.Equal(
            new[] { "GBR", "IND", "PAK" },
            PassportNumberFormats.IssuingStates.Order().ToArray());
}
