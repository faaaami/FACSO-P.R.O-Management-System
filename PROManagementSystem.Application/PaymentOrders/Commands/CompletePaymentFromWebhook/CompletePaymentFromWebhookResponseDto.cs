using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.PaymentOrders.Commands.CompletePaymentFromWebhook;

/// <summary>
/// Result of a verified webhook delivery. Every verified delivery is
/// acknowledged with 200; <see cref="Outcome"/> says what was done.
/// </summary>
public sealed record CompletePaymentFromWebhookResponseDto(
    Guid? PaymentOrderId,
    string? RazorpayOrderId,
    PaymentStatus? Status,
    DateTime? PaidAt,
    string EventId,
    bool Completed,
    string Outcome
);

/// <summary>Values of <see cref="CompletePaymentFromWebhookResponseDto.Outcome"/>.</summary>
public static class WebhookOutcomes
{
    /// <summary>The invoice was settled by this payment.</summary>
    public const string Completed = "Completed";

    /// <summary>Money was captured but could not be applied; admins notified.</summary>
    public const string RequiresReview = "RequiresReview";

    /// <summary>The order was marked Failed.</summary>
    public const string Failed = "Failed";

    /// <summary>This payment was already applied; nothing changed.</summary>
    public const string AlreadyProcessed = "AlreadyProcessed";

    /// <summary>The event id was seen before; nothing changed.</summary>
    public const string Duplicate = "Duplicate";

    /// <summary>The event is not one this system acts on.</summary>
    public const string Ignored = "Ignored";
}
