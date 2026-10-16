using PROManagementSystem.Application.Common.Validation;
using Xunit;

namespace PROManagementSystem.Tests.Validation;

/// <summary>784-YYYY-NNNNNNN-N format, birth-year and check-digit warnings.</summary>
public sealed class EmiratesIdNumberTests
{
    // Synthetic number whose last digit satisfies the Luhn check.
    private const string Valid = "784-1990-1234567-6";

    [Theory(DisplayName = "Accepted spellings normalise to 784-YYYY-NNNNNNN-N")]
    [InlineData("784-1990-1234567-6")]
    [InlineData("784199012345676")]
    [InlineData("784 1990 1234567 6")]
    [InlineData(" 784-1990-1234567-6 ")]
    public void Normalize_AcceptsCommonSpellings(string input)
        => Assert.Equal(Valid, EmiratesIdNumber.Normalize(input));

    [Theory(DisplayName = "Wrong numbers are rejected")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("785-1990-1234567-6")]
    [InlineData("784-1990-123456-6")]
    [InlineData("784-1990-12345678-6")]
    [InlineData("784-199O-1234567-6")]
    [InlineData("784/1990/1234567/6")]
    public void Normalize_RejectsWrongNumbers(string? input)
    {
        Assert.Null(EmiratesIdNumber.Normalize(input));
        Assert.False(EmiratesIdNumber.IsValidFormat(input));
    }

    [Fact(DisplayName = "A correct number with a matching birth year has no warnings")]
    public void Valid_NoWarnings()
        => Assert.Empty(EmiratesIdNumber.Warnings(Valid, new DateOnly(1990, 1, 15)));

    [Fact(DisplayName = "A birth-year mismatch is a warning")]
    public void BirthYearMismatch_Warns()
    {
        var warning = Assert.Single(
            EmiratesIdNumber.Warnings(Valid, new DateOnly(1991, 1, 15)));

        Assert.Contains("date of birth", warning);
    }

    [Fact(DisplayName = "A failed check digit is a warning, not an error")]
    public void LuhnFailure_Warns()
    {
        Assert.True(EmiratesIdNumber.IsValidFormat("784-1990-1234567-5"));

        var warning = Assert.Single(EmiratesIdNumber.Warnings("784-1990-1234567-5", null));
        Assert.Contains("last digit", warning);
    }

    [Fact(DisplayName = "The number is found in free OCR text")]
    public void FindInText_FindsNumber()
        => Assert.Equal(
            Valid,
            EmiratesIdNumber.FindInText("ID Number / رقم الهوية\n784-1990-1234567-6\nName: X"));

    [Fact(DisplayName = "Free text without a number returns null")]
    public void FindInText_NoNumber()
        => Assert.Null(EmiratesIdNumber.FindInText("Resident Identity Card"));
}
