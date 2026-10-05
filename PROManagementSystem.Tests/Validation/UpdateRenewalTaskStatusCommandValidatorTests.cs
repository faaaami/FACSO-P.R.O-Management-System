using FluentValidation;
using PROManagementSystem.Application.Tasks.Commands.UpdateRenewalTaskStatus;
using Xunit;

namespace PROManagementSystem.Tests.Validation;

/// <summary>
/// Unit tests for <see cref="UpdateRenewalTaskStatusCommandValidator"/>, the
/// validation gate on PATCH /api/v1/{agent|admin}/tasks/{taskId}/status.
///
/// Pure unit tests: no database, no HTTP, no Testcontainers, so these run in
/// the same pass as the other validator tests. The persistence, audit and
/// document-write behaviour is covered separately by RenewedExpiryDateTests.
///
/// These exist to pin the CONDITIONAL nature of the rule. The date is required
/// for exactly one target status. A rule that demanded it unconditionally would
/// break every earlier transition; a rule that never demanded it would leave the
/// completion path unprotected. Asserting the negative cases for the other
/// statuses is what distinguishes the two.
/// </summary>
public class UpdateRenewalTaskStatusCommandValidatorTests
{
    private static readonly Guid TaskId =
        Guid.Parse("00000000000000000000000000000501");

    private static readonly UpdateRenewalTaskStatusCommandValidator Validator =
        new();

    private static readonly DateTime FutureExpiry =
        DateTime.UtcNow.AddYears(1);

    private static UpdateRenewalTaskStatusCommand With(
        string status,
        DateTime? renewedExpiryDate)
        => new(TaskId, status, null, renewedExpiryDate);

    private static bool IsValid(string status, DateTime? renewedExpiryDate)
        => Validator.Validate(With(status, renewedExpiryDate)).IsValid;

    private static string[] FailedProperties(
        string status,
        DateTime? renewedExpiryDate)
        => [.. Validator
            .Validate(With(status, renewedExpiryDate))
            .Errors
            .Select(e => e.PropertyName)];

    // ------------------------------------------------------------------
    // Required for Updated
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A future renewed expiry is valid when completing")]
    public void AFutureExpiry_IsValidWhenCompleting()
    {
        Assert.True(
            IsValid("Updated", FutureExpiry));
    }

    [Fact(DisplayName =
        "A missing renewed expiry is invalid when completing")]
    public void AMissingExpiry_IsInvalidWhenCompleting()
    {
        Assert.False(
            IsValid("Updated", null));
    }

    [Fact(DisplayName =
        "The violation is raised against RenewedExpiryDate, so the caller is " +
        "told which field to fix")]
    public void TheViolation_NamesTheRenewedExpiryDateField()
    {
        // The response body carries this property name. Naming Status or Note
        // instead would point the Admin at the field they got right.
        Assert.Contains(
            "RenewedExpiryDate",
            FailedProperties("Updated", null));
    }

    [Fact(DisplayName =
        "A past renewed expiry is invalid when completing")]
    public void APastExpiry_IsInvalidWhenCompleting()
    {
        Assert.False(
            IsValid("Updated", DateTime.UtcNow.AddDays(-1)));
    }

    [Fact(DisplayName =
        "A renewed expiry equal to now is invalid — the rule is strictly " +
        "in the future")]
    public void AnExpiryOfNow_IsInvalidWhenCompleting()
    {
        Assert.False(
            IsValid("Updated", DateTime.UtcNow));
    }

    // ------------------------------------------------------------------
    // Required for nothing else
    // ------------------------------------------------------------------

    [Theory(DisplayName =
        "A non-completing status is valid with no renewed expiry")]
    [InlineData("Submitted")]
    [InlineData("FeePaid")]
    [InlineData("AwaitingApproval")]
    [InlineData("Approved")]
    [InlineData("Blocked")]
    public void ANonCompletingStatus_NeedsNoExpiry(string status)
    {
        Assert.True(
            IsValid(status, null));
    }

    [Fact(DisplayName =
        "A non-completing status is still valid if a renewed expiry happens " +
        "to be supplied — the field is not rejected, only ignored")]
    public void ANonCompletingStatus_IgnoresASuppliedExpiry()
    {
        // Refusing a harmless extra field would be a gratuitous break, and the
        // handler does not apply the date on these paths.
        Assert.True(
            IsValid("Approved", FutureExpiry));
    }

    [Fact(DisplayName =
        "A past renewed expiry is ignored on a non-completing status")]
    public void APastExpiry_IsIgnoredOnANonCompletingStatus()
    {
        Assert.True(
            IsValid("FeePaid", DateTime.UtcNow.AddYears(-5)));
    }

    [Fact(DisplayName =
        "The rule matches the status case-insensitively, like the handler")]
    public void TheRule_MatchesStatusCaseInsensitively()
    {
        // The handler parses with ignoreCase, so the validator must agree or a
        // lowercase "updated" would pass validation and then behave
        // differently inside the handler.
        Assert.False(
            IsValid("updated", null));
        Assert.True(
            IsValid("updated", FutureExpiry));
    }

    [Fact(DisplayName =
        "The pre-existing status rule is unchanged")]
    public void TheStatusRule_IsUnchanged()
    {
        // Guard against the conditional date rule disturbing the original
        // contract: an empty or unknown status is still invalid.
        Assert.False(IsValid("", FutureExpiry));
        Assert.False(IsValid("   ", FutureExpiry));
        Assert.False(IsValid("NotAStatus", FutureExpiry));
    }

    // ------------------------------------------------------------------
    // P10.1 / R-11 - UTC DATE semantics
    // ------------------------------------------------------------------

    /// <summary>
    /// P10.1 R-11 UNIT REGRESSION SUITE.
    ///
    /// THE DEFECT
    ///
    /// The rule compared the RAW bound value against <c>DateTime.UtcNow</c>,
    /// while persistence keeps only the DATE component
    /// (<c>ToUtcExpiryDate</c> -&gt; <c>DateTime.SpecifyKind(value.Date,
    /// DateTimeKind.Utc)</c>). A value carrying TODAY's date with a time of day
    /// later than the current clock therefore passed validation and then
    /// persisted as today's UTC midnight, which is already expired.
    ///
    /// THE RULE NOW
    ///
    /// <c>Value.Date &gt; DateTime.UtcNow.Date</c> — the normalized date must be
    /// strictly after today's UTC date. Time of day is irrelevant, so the
    /// outcome does not depend on when the suite runs.
    ///
    /// Every input is built from <c>DateTime.UtcNow.Date</c> plus whole days, so
    /// these tests are valid at any hour of any day. V1 is the discriminator
    /// that fails against the old timestamp comparison: it is a value that only
    /// looks "future" because of its time component.
    /// </summary>
    private static DateTime UtcToday => DateTime.UtcNow.Date;

    [Fact(DisplayName =
        "R11-V1: today at 23:59:59 is invalid — a later TIME of day must " +
        "not make today look like the future")]
    public void TodayLateInTheDay_IsInvalidWhenCompleting()
    {
        var todayLateInTheDay =
            UtcToday.AddHours(23).AddMinutes(59).AddSeconds(59);

        Assert.False(
            IsValid("Updated", todayLateInTheDay),
            "A renewed expiry whose DATE is today must be refused. " +
            "Persistence stores only the date, so accepting this would " +
            "persist today's UTC midnight, which is already expired.");
    }

    [Fact(DisplayName =
        "R11-V2: today at midnight is invalid")]
    public void TodayAtMidnight_IsInvalidWhenCompleting()
    {
        Assert.False(
            IsValid("Updated", UtcToday));
    }

    [Fact(DisplayName =
        "R11-V3: yesterday is invalid")]
    public void Yesterday_IsInvalidWhenCompleting()
    {
        Assert.False(
            IsValid("Updated", UtcToday.AddDays(-1)));
    }

    [Fact(DisplayName =
        "R11-V4: tomorrow is valid — one day past the boundary is the " +
        "smallest accepted date")]
    public void Tomorrow_IsValidWhenCompleting()
    {
        Assert.True(
            IsValid("Updated", UtcToday.AddDays(1)));
    }

    [Fact(DisplayName =
        "R11-V5: a clearly future date is valid")]
    public void AClearlyFutureDate_IsValidWhenCompleting()
    {
        Assert.True(
            IsValid("Updated", DateTime.UtcNow.AddYears(1)));
    }
}
