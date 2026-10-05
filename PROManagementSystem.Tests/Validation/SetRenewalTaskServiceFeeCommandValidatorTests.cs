using FluentValidation;
using PROManagementSystem.Application.Tasks.Commands.SetRenewalTaskServiceFee;
using Xunit;

namespace PROManagementSystem.Tests.Validation;

/// <summary>
/// Unit tests for <see cref="SetRenewalTaskServiceFeeCommandValidator"/>, the
/// validation gate on PATCH /api/v1/admin/tasks/{taskId}/service-fee.
///
/// These are pure unit tests: no database, no HTTP, no Testcontainers, so they
/// run in the same pass as the other validator tests. Persistence, history, audit
/// and the terminal-state guard are covered separately by the endpoint tests.
///
/// The rules exist because the amount is not a free-form number. It is copied
/// verbatim onto the Service Fee invoice created when the task completes, and
/// that invoice column is numeric(18,2) — so a third decimal place would be
/// rounded at billing time, and a value at or below zero would persist a figure
/// that the completion gate then refuses. Both are caught here, at the door,
/// where the Admin can still fix them.
/// </summary>
public class SetRenewalTaskServiceFeeCommandValidatorTests
{
    private static readonly Guid TaskId =
        Guid.Parse("00000000000000000000000000000501");

    private static readonly SetRenewalTaskServiceFeeCommandValidator Validator = new();

    private static SetRenewalTaskServiceFeeCommand With(decimal amount)
        => new(TaskId, amount);

    private static bool IsValid(decimal amount)
        => Validator.Validate(With(amount)).IsValid;

    /// <summary>
    /// The property names a violation was raised against, so a test can assert
    /// WHICH rule refused a value rather than only that something did.
    /// </summary>
    private static string[] FailedProperties(decimal amount)
        => [.. Validator.Validate(With(amount)).Errors.Select(e => e.PropertyName)];

    // ------------------------------------------------------------------
    // Greater than 0
    // ------------------------------------------------------------------

    [Fact]
    public void Accepts_A_Positive_Amount()
    {
        Assert.True(IsValid(1500m));
    }

    [Fact]
    public void Accepts_The_Smallest_Representable_Positive_Amount()
    {
        // 0.01m is the smallest figure the numeric(18,2) column can store, so it
        // is the boundary case a "greater than zero" rule has to get right.
        Assert.True(IsValid(0.01m));
    }

    [Fact]
    public void Rejects_Zero()
    {
        // Deliberately stricter than CreateRetainerInvoiceCommandValidator, which
        // admits zero. A zero here would persist cleanly and then be refused by
        // the completion gate, so it is rejected at the door instead.
        Assert.Contains("Amount", FailedProperties(0m));
    }

    [Fact]
    public void Rejects_Negative_Amount()
    {
        Assert.Contains("Amount", FailedProperties(-1m));
    }

    // ------------------------------------------------------------------
    // Two decimal places
    // ------------------------------------------------------------------

    [Fact]
    public void Accepts_One_And_Two_Decimal_Places()
    {
        Assert.True(IsValid(1500.5m));
        Assert.True(IsValid(1500.55m));
    }

    [Fact]
    public void Accepts_A_Whole_Number_With_No_Decimal_Point()
    {
        // The "no separator at all" branch is a separate path from the one that
        // counts places, and a whole number is the most common input by far.
        Assert.True(IsValid(2000m));
    }

    [Fact]
    public void Rejects_Three_Decimal_Places()
    {
        // 1500.555 is exactly the case that would be silently rounded to 1500.56
        // (or 1500.55, depending on the rounding mode) on its way to the invoice.
        Assert.Contains("Amount", FailedProperties(1500.555m));
    }

    [Fact]
    public void Rejects_A_Value_That_Is_Whole_But_Carries_A_Third_Decimal_Place()
    {
        // 10.100m is numerically identical to 10.1m and both would pass an
        // arithmetic check, but the literal scale is three. The validator reads
        // the text form, so it refuses this — see HasAtMostTwoDecimalPlaces.
        Assert.Contains("Amount", FailedProperties(10.100m));
    }

    [Fact]
    public void Rejects_A_Small_Amount_With_Excess_Precision()
    {
        // Guards the rounding case at the other end of the range, where a
        // truncation would take a non-zero fee to zero.
        Assert.Contains("Amount", FailedProperties(0.005m));
    }

    [Fact]
    public void Accepts_A_Whole_Number_Whose_Magnitude_Is_Large()
    {
        // There is no business maximum on this amount, and a large whole figure
        // must not be refused by the scale rule by accident. It is far above
        // anything sensible, which is the point: the endpoint is not a place for
        // a second, quietly divergent ceiling.
        Assert.True(IsValid(999_999_999_999m));
    }

    // ------------------------------------------------------------------
    // Combined
    // ------------------------------------------------------------------

    [Fact]
    public void Reports_Both_Rules_When_An_Amount_Violates_Them_At_Once()
    {
        // A negative value that also carries a third decimal place is a typo
        // rather than a decision, and the reader is better served by both
        // messages than by whichever rule happened to run first.
        Assert.Equal(
            ["Amount", "Amount"],
            FailedProperties(-1.005m));
    }
}
