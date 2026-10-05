using FluentValidation;
using PROManagementSystem.Application.Tasks.Commands.BlockRenewalTask;
using Xunit;

namespace PROManagementSystem.Tests.Validation;

/// <summary>
/// Unit tests for <see cref="BlockRenewalTaskCommandValidator"/>, the validation
/// gate on PATCH /api/v1/agent/tasks/{taskId}/block.
///
/// Pure unit tests: no database, no HTTP, no Testcontainers, so they run in the
/// same pass as the other validator tests. That the reason actually reaches
/// renewal_tasks.blocked_reason, renewal_task_histories.note and
/// audit_logs.description is covered by the endpoint tests.
///
/// The rule exists because a block reason is written to three columns with two
/// different limits. blocked_reason and the history note are varchar(1000), but
/// audit_logs.description is varchar(500) and is composed as
/// "Renewal task was blocked: {reason}" — a 28-character prefix. Postgres
/// refuses an over-length value with 22001, and because all three writes share
/// one SaveChanges, an unbounded reason failed the whole block with a 500
/// instead of failing the single field the reader got wrong. The sibling
/// UnblockRenewalTask handler already bounds itself at 400 for the identical
/// reason; this closes the same hole on the write side.
/// </summary>
public class BlockRenewalTaskCommandValidatorTests
{
    private static readonly Guid TaskId =
        Guid.Parse("00000000000000000000000000000501");

    private static readonly BlockRenewalTaskCommandValidator Validator = new();

    private static BlockRenewalTaskCommand With(string? reason)
        => new(TaskId, reason!);

    private static bool IsValid(string? reason)
        => Validator.Validate(With(reason)).IsValid;

    /// <summary>
    /// The property a violation was raised against, so a test can assert WHICH
    /// rule refused a value rather than only that something did.
    /// </summary>
    private static string[] FailedProperties(string? reason)
        => [.. Validator.Validate(With(reason)).Errors
            .Select(e => e.PropertyName)];

    // ------------------------------------------------------------------
    // Required
    // ------------------------------------------------------------------

    [Fact]
    public void Rejects_Null()
    {
        // An omitted JSON property binds to null. The handler catches it too,
        // but a field-level VALIDATION_ERROR names the field, where a thrown
        // ArgumentException does not.
        Assert.Contains("BlockedReason", FailedProperties(null));
    }

    [Fact]
    public void Rejects_Empty()
    {
        Assert.Contains("BlockedReason", FailedProperties(string.Empty));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData(" \t\n ")]
    public void Rejects_Whitespace_Only(string reason)
    {
        // Whitespace is not a reason. IsNullOrWhiteSpace in the handler and
        // NotEmpty here must agree, or the same input would be a 400 from one
        // layer and a 500 from the other depending on which ran.
        Assert.Contains("BlockedReason", FailedProperties(reason));
    }

    // ------------------------------------------------------------------
    // A valid short reason
    // ------------------------------------------------------------------

    [Fact]
    public void Accepts_A_Valid_Short_Reason()
    {
        Assert.True(IsValid("Waiting on the client to supply the original visa scan."));
    }

    [Fact]
    public void Accepts_A_Single_Character()
    {
        // The lower boundary. A rule written as "at least N characters" would
        // wrongly refuse this, and a one-character reason is still a reason.
        Assert.True(IsValid("x"));
    }

    // ------------------------------------------------------------------
    // The 400-character bound
    // ------------------------------------------------------------------

    [Fact]
    public void Accepts_Exactly_The_Limit()
    {
        // The boundary must be reachable. This is the test that fails if the
        // rule is off by one, and it is the value that comes closest to the
        // varchar(500) audit description.
        Assert.True(IsValid(new string('x', 400)));
    }

    [Fact]
    public void Rejects_One_Over_The_Limit()
    {
        Assert.Contains(
            "BlockedReason",
            FailedProperties(new string('x', 401)));
    }

    [Fact]
    public void Rejects_A_Far_Over_Limit_Reason()
    {
        // The 500-char column is the real ceiling: 400 characters of reason plus
        // the 28-character prefix is 428, but a 600-character reason would
        // overflow description(500) even though it fits note(1000). The bound
        // refuses the whole class, not just its first character.
        Assert.Contains(
            "BlockedReason",
            FailedProperties(new string('x', 600)));
    }

    [Fact]
    public void Rejects_A_Reason_Long_Enough_To_Overflow_The_Audit_Column_On_Its_Own()
    {
        // 473 is the exact point at which "Renewal task was blocked: " + reason
        // exceeds description varchar(500). It is called out separately because
        // it is the value that used to produce PostgreSQL 22001 and a 500.
        Assert.Contains(
            "BlockedReason",
            FailedProperties(new string('x', 473)));
    }

    // ------------------------------------------------------------------
    // Trim semantics
    // ------------------------------------------------------------------

    [Fact]
    public void Accepts_A_Padded_Reason_That_Is_Exactly_The_Limit_Once_Trimmed()
    {
        // The handler trims once before storing, so the trimmed value is what
        // reaches the columns. Measuring the raw text would refuse this input and
        // disagree with what is actually written. Same reasoning as
        // UnblockRenewalTask, which measures the trimmed value for this reason.
        var padded = "  " + new string('x', 400) + "  ";

        Assert.True(padded.Length > 400);
        Assert.True(IsValid(padded));
    }

    [Fact]
    public void Rejects_A_Padded_Reason_That_Is_Over_The_Limit_Once_Trimmed()
    {
        // The other half: padding must not become a way round the bound. The
        // trimmed value is 401 here, so it is refused even though the trim
        // removes two characters from the raw length.
        var padded = " " + new string('x', 401) + " ";

        Assert.Contains("BlockedReason", FailedProperties(padded));
    }

    [Fact]
    public void Rejects_A_Padded_Whitespace_Only_Reason()
    {
        // Whitespace survives a length check comfortably but is still not a
        // reason, so the required rule has to carry this case on its own.
        Assert.Contains("BlockedReason", FailedProperties("          "));
    }

    // ------------------------------------------------------------------
    // Combined
    // ------------------------------------------------------------------

    [Fact]
    public void Reports_The_Required_Rule_Alone_For_A_Blank_Reason()
    {
        // A blank reason is not also a too-long one, so the reader is told the
        // single thing that is actually wrong.
        Assert.Equal(
            ["BlockedReason"],
            FailedProperties("   "));
    }
}
