using PROMangementSystem.Domain.Common;

namespace PROManagementSystem.Domain.Entities;

/// <summary>
/// One processed payment-gateway webhook delivery. The unique EventId makes a
/// redelivered event a no-op.
/// </summary>
public sealed class WebhookEvent : BaseEntity
{
    public string EventId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string? RazorpayOrderId { get; set; }

    public string? RazorpayPaymentId { get; set; }

    public string Outcome { get; set; } = string.Empty;
}
