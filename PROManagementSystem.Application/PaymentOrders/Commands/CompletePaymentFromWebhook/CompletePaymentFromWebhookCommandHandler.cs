using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Notifications;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.PaymentOrders.Commands.CompletePaymentFromWebhook;

/// <summary>
/// Applies a verified Razorpay webhook event.
/// </summary>
/// <remarks>
/// A verified event is never rejected: rejecting makes Razorpay retry and
/// eventually disable the webhook, while the customer's money stays captured
/// with no local record. Instead:
/// <list type="bullet">
/// <item><c>payment.captured</c> / <c>order.paid</c> settle the invoice when
/// it is still payable. Otherwise the capture is recorded on the order, the
/// order becomes <see cref="PaymentStatus.PaidRequiresReview"/>, the invoice
/// is left alone and admins are notified.</item>
/// <item><c>payment.failed</c> marks an open order Failed, so a new attempt
/// can be made.</item>
/// <item>Any other event is acknowledged and ignored.</item>
/// </list>
/// Each event id is stored in <c>webhook_events</c> (unique), so a redelivery
/// is a no-op even when it races the original.
/// </remarks>
public sealed class CompletePaymentFromWebhookCommandHandler
    : IRequestHandler<
        CompletePaymentFromWebhookCommand,
        CompletePaymentFromWebhookResponseDto>
{
    private const string CapturedEvent = "payment.captured";
    private const string OrderPaidEvent = "order.paid";
    private const string FailedEvent = "payment.failed";
    private const string CapturedStatus = "captured";

    private const string ReviewTitle = "Payment needs review";

    private readonly IApplicationDbContext _context;
    private readonly IUserNotifier _userNotifier;
    private readonly ILogger<CompletePaymentFromWebhookCommandHandler>
        _logger;

    public CompletePaymentFromWebhookCommandHandler(
        IApplicationDbContext context,
        IUserNotifier userNotifier,
        ILogger<CompletePaymentFromWebhookCommandHandler> logger)
    {
        _context = context;
        _userNotifier = userNotifier;
        _logger = logger;
    }

    public async Task<CompletePaymentFromWebhookResponseDto> Handle(
        CompletePaymentFromWebhookCommand request,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _context.BeginTransactionAsync(cancellationToken);

        try
        {
            var now = DateTime.UtcNow;

            // 1. Claim the event id. A concurrent redelivery blocks on the
            //    unique index until this transaction ends, then fails here.
            var webhookEvent =
                await TryClaimEventAsync(request, now, cancellationToken);

            if (webhookEvent is null &&
                !string.IsNullOrWhiteSpace(request.WebhookEventId))
            {
                await transaction.RollbackAsync(cancellationToken);

                return Result(request, null, WebhookOutcomes.Duplicate);
            }

            // 2. Dispatch on the event type.
            var eventName = request.PaymentEventName.Trim().ToLowerInvariant();

            var result = eventName switch
            {
                CapturedEvent or OrderPaidEvent =>
                    await HandleCapturedAsync(request, now, cancellationToken),
                FailedEvent =>
                    await HandleFailedAsync(request, now, cancellationToken),
                _ => Result(request, null, WebhookOutcomes.Ignored)
            };

            if (webhookEvent is not null)
            {
                webhookEvent.Outcome = result.Outcome;
                webhookEvent.UpdatedAt = now;
            }

            // 3. One atomic commit: event row, order, invoice, audit rows and
            //    notifications.
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // ------------------------------------------------------------------
    // Idempotency
    // ------------------------------------------------------------------

    /// <summary>
    /// Inserts the event row. Returns null when the event id was already
    /// stored (or when Razorpay sent no event id, which is logged).
    /// </summary>
    private async Task<WebhookEvent?> TryClaimEventAsync(
        CompletePaymentFromWebhookCommand request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.WebhookEventId))
        {
            _logger.LogWarning(
                "Razorpay webhook '{EventName}' for order {RazorpayOrderId} " +
                "has no event id; relying on order state for idempotency.",
                request.PaymentEventName,
                request.RazorpayOrderId);

            return null;
        }

        var webhookEvent = new WebhookEvent
        {
            Id = Guid.NewGuid(),
            EventId = request.WebhookEventId.Trim(),
            EventType = Truncate(request.PaymentEventName, 100),
            RazorpayOrderId = request.RazorpayOrderId,
            RazorpayPaymentId = request.RazorpayPaymentId,
            Outcome = "Processing",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };

        _context.WebhookEvents.Add(webhookEvent);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateEventViolation(ex))
        {
            _context.WebhookEvents.Remove(webhookEvent);

            _logger.LogInformation(
                "Razorpay webhook event {EventId} was already processed.",
                request.WebhookEventId);

            return null;
        }

        return webhookEvent;
    }

    // ------------------------------------------------------------------
    // payment.captured / order.paid
    // ------------------------------------------------------------------

    private async Task<CompletePaymentFromWebhookResponseDto> HandleCapturedAsync(
        CompletePaymentFromWebhookCommand request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RazorpayOrderId) ||
            string.IsNullOrWhiteSpace(request.RazorpayPaymentId) ||
            !request.AmountInSmallestUnit.HasValue ||
            string.IsNullOrWhiteSpace(request.Currency))
        {
            _logger.LogWarning(
                "Razorpay webhook {EventId} ('{EventName}') is missing payment " +
                "fields and was ignored.",
                request.WebhookEventId,
                request.PaymentEventName);

            return Result(request, null, WebhookOutcomes.Ignored);
        }

        // Only a captured payment moves money. An authorized-only payment is
        // followed by its own payment.captured event.
        if (!string.Equals(
                request.PaymentStatus,
                CapturedStatus,
                StringComparison.OrdinalIgnoreCase))
        {
            return Result(request, null, WebhookOutcomes.Ignored);
        }

        var capturedAmount = request.AmountInSmallestUnit.Value / 100m;

        // 1. Row-lock the order (serialisation point for concurrent webhooks
        //    and manual settlement of the same order).
        var order = await LockOrderAsync(
            request.RazorpayOrderId, cancellationToken);

        if (order is null)
        {
            _logger.LogError(
                "Razorpay captured payment {PaymentId} ({Amount} {Currency}) " +
                "for unknown order {RazorpayOrderId}.",
                request.RazorpayPaymentId,
                capturedAmount,
                request.Currency,
                request.RazorpayOrderId);

            await _userNotifier.NotifyAdminsAsync(
                ReviewTitle,
                $"Payment {request.RazorpayPaymentId} of {capturedAmount:0.00} " +
                $"{request.Currency} was received for Razorpay order " +
                $"{request.RazorpayOrderId}, which does not match any payment " +
                "order: refund or allocate.",
                cancellationToken);

            return Result(request, null, WebhookOutcomes.RequiresReview);
        }

        // 2. This order already holds a captured payment.
        if (order.Status is PaymentStatus.Paid or PaymentStatus.PaidRequiresReview &&
            order.RazorpayPaymentId is not null)
        {
            if (order.RazorpayPaymentId == request.RazorpayPaymentId)
            {
                return Result(request, order, WebhookOutcomes.AlreadyProcessed);
            }

            // A second capture on the same gateway order. The order can only
            // hold one payment id, so the extra payment lives in the audit
            // row and the admin notification.
            AddAudit(
                "PaymentRequiresReview",
                order,
                $"A second payment '{request.RazorpayPaymentId}' of " +
                $"{capturedAmount:0.00} {request.Currency} was captured on " +
                $"Razorpay order '{order.RazorpayOrderId}', which was already " +
                $"paid by '{order.RazorpayPaymentId}'.",
                request,
                now);

            await _userNotifier.NotifyAdminsAsync(
                ReviewTitle,
                $"A second payment of {capturedAmount:0.00} {request.Currency} " +
                $"was received on Razorpay order {order.RazorpayOrderId}: " +
                "refund or allocate.",
                cancellationToken);

            return Result(request, order, WebhookOutcomes.RequiresReview);
        }

        // 3. Lock any OTHER open order of the same invoice before the
        //    invoice row, so settlement can retire it without deadlocking
        //    against a manual mark-paid (which locks order, then invoice).
        var invoiceId = order.InvoiceType == PaymentInvoiceType.RetainerInvoice
            ? order.RetainerInvoiceId
            : order.ServiceFeeInvoiceId;

        var otherOpenOrders = invoiceId.HasValue
            ? await OpenPaymentOrders.LockOpenOrdersAsync(
                _context,
                order.InvoiceType,
                invoiceId.Value,
                excludeOrderId: order.Id,
                cancellationToken)
            : new List<PaymentOrder>();

        // 4. Lock the invoice derived from the persisted order (never from
        //    request data) and decide whether the money can be applied.
        var invoice = await LockInvoiceAsync(order, cancellationToken);

        var reviewReason = invoice is null
            ? "The payment order has no payable invoice (missing or deleted)."
            : ReviewReasonFor(order, invoice, request, capturedAmount);

        // 5. Always keep the evidence of the captured money on the order.
        order.RazorpayPaymentId = request.RazorpayPaymentId;
        order.RazorpaySignature = request.RazorpaySignature;
        order.CapturedAmount = capturedAmount;
        order.CapturedCurrency = Truncate(request.Currency.ToUpperInvariant(), 10);
        order.PaidAt = now;
        order.UpdatedAt = now;

        if (reviewReason is not null)
        {
            order.Status = PaymentStatus.PaidRequiresReview;
            order.ReviewReason = Truncate(reviewReason, 500);

            AddAudit(
                "PaymentRequiresReview",
                order,
                $"Payment '{request.RazorpayPaymentId}' of {capturedAmount:0.00} " +
                $"{request.Currency} on Razorpay order '{order.RazorpayOrderId}' " +
                $"was not applied: {reviewReason}",
                request,
                now);

            await _userNotifier.NotifyAdminsAsync(
                ReviewTitle,
                $"{ReviewMessageFor(invoice)} Razorpay order " +
                $"{order.RazorpayOrderId}, payment {request.RazorpayPaymentId}, " +
                $"{capturedAmount:0.00} {request.Currency}.",
                cancellationToken);

            return Result(request, order, WebhookOutcomes.RequiresReview);
        }

        // 6. Settle: order and invoice Paid with one shared timestamp.
        order.Status = PaymentStatus.Paid;
        order.ReviewReason = null;
        invoice!.MarkPaid(now);

        AddAudit(
            "PaymentCompletedFromWebhook",
            order,
            $"Payment order '{order.RazorpayOrderId}' was marked as paid via " +
            "Razorpay webhook.",
            request,
            now);

        // 7. A receipt for the client and a heads-up for the admins. Saved in
        //    this transaction and pushed only after it commits.
        var paid = NotificationLabels.Money(capturedAmount, order.Currency);

        await _userNotifier.NotifyClientContactsAsync(
            order.ClientCompanyId,
            "Payment received",
            $"We received your payment of {paid} for invoice " +
            $"{invoice.Number}. Thank you.",
            cancellationToken);

        await _userNotifier.NotifyAdminsAsync(
            "Online payment received",
            $"Invoice {invoice.Number} was paid online ({paid}).",
            cancellationToken);

        // 8. A payment on this order settled the invoice, so any other open
        //    checkout for it must stop being payable.
        foreach (var other in otherOpenOrders)
        {
            OpenPaymentOrders.CancelForSettlement(
                _context,
                other,
                userId: null,
                $"the invoice was paid through Razorpay order '{order.RazorpayOrderId}'",
                now);
        }

        return Result(request, order, WebhookOutcomes.Completed);
    }

    private static string? ReviewReasonFor(
        PaymentOrder order,
        LockedInvoice invoice,
        CompletePaymentFromWebhookCommand request,
        decimal capturedAmount)
    {
        if (!invoice.IsPending)
        {
            return $"The invoice was already {invoice.Status} when the payment " +
                   "arrived.";
        }

        if (capturedAmount != order.Amount)
        {
            return $"Captured amount {capturedAmount:0.00} does not match the " +
                   $"order amount {order.Amount:0.00}.";
        }

        if (!string.Equals(
                request.Currency,
                order.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            return $"Captured currency {request.Currency} does not match the " +
                   $"order currency {order.Currency}.";
        }

        return null;
    }

    private static string ReviewMessageFor(LockedInvoice? invoice) =>
        invoice is not null && !invoice.IsPending
            ? "Payment received for an invoice that was already settled: " +
              "refund or allocate."
            : "Payment received that could not be applied to its invoice: " +
              "refund or allocate.";

    // ------------------------------------------------------------------
    // payment.failed
    // ------------------------------------------------------------------

    private async Task<CompletePaymentFromWebhookResponseDto> HandleFailedAsync(
        CompletePaymentFromWebhookCommand request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RazorpayOrderId))
        {
            return Result(request, null, WebhookOutcomes.Ignored);
        }

        var order = await LockOrderAsync(
            request.RazorpayOrderId, cancellationToken);

        // A failure only closes an order that is still open. A later
        // successful retry on the same gateway order still settles the
        // invoice through the captured path.
        if (order is null || !OpenPaymentOrders.IsOpen(order.Status))
        {
            return Result(request, order, WebhookOutcomes.Ignored);
        }

        order.Status = PaymentStatus.Failed;
        order.FailedAt = now;
        order.UpdatedAt = now;

        AddAudit(
            "PaymentOrderFailed",
            order,
            $"Payment '{request.RazorpayPaymentId}' on Razorpay order " +
            $"'{order.RazorpayOrderId}' failed; the order was marked Failed.",
            request,
            now);

        return Result(request, order, WebhookOutcomes.Failed);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<PaymentOrder?> LockOrderAsync(
        string razorpayOrderId,
        CancellationToken cancellationToken)
    {
        var lockedRows = await _context.PaymentOrders
            .FromSqlInterpolated(
                $"SELECT * FROM payment_orders WHERE \"RazorpayOrderId\" = {razorpayOrderId} AND \"IsDeleted\" = false FOR UPDATE")
            .ToListAsync(cancellationToken);

        return lockedRows.SingleOrDefault();
    }

    private async Task<LockedInvoice?> LockInvoiceAsync(
        PaymentOrder order,
        CancellationToken cancellationToken)
    {
        if (order.InvoiceType == PaymentInvoiceType.RetainerInvoice &&
            order.RetainerInvoiceId.HasValue)
        {
            var rows = await _context.RetainerInvoices
                .FromSqlInterpolated(
                    $"SELECT * FROM retainer_invoices WHERE \"Id\" = {order.RetainerInvoiceId.Value} AND \"IsDeleted\" = false FOR UPDATE")
                .ToListAsync(cancellationToken);

            var invoice = rows.SingleOrDefault();

            return invoice is null
                ? null
                : new LockedInvoice(
                    invoice.InvoiceNumber,
                    invoice.Status.ToString(),
                    invoice.Status == RetainerInvoiceStatus.Pending,
                    paidAt =>
                    {
                        invoice.Status = RetainerInvoiceStatus.Paid;
                        invoice.PaidAt = paidAt;
                        invoice.UpdatedAt = paidAt;
                    });
        }

        if (order.InvoiceType == PaymentInvoiceType.ServiceFeeInvoice &&
            order.ServiceFeeInvoiceId.HasValue)
        {
            var rows = await _context.ServiceFeeInvoices
                .FromSqlInterpolated(
                    $"SELECT * FROM service_fee_invoices WHERE \"Id\" = {order.ServiceFeeInvoiceId.Value} AND \"IsDeleted\" = false FOR UPDATE")
                .ToListAsync(cancellationToken);

            var invoice = rows.SingleOrDefault();

            return invoice is null
                ? null
                : new LockedInvoice(
                    invoice.InvoiceNumber,
                    invoice.Status.ToString(),
                    invoice.Status == ServiceFeeInvoiceStatus.Pending,
                    paidAt =>
                    {
                        invoice.Status = ServiceFeeInvoiceStatus.Paid;
                        invoice.PaidAt = paidAt;
                        invoice.UpdatedAt = paidAt;
                    });
        }

        // The order's invoice type and invoice id disagree.
        return null;
    }

    private void AddAudit(
        string action,
        PaymentOrder order,
        string description,
        CompletePaymentFromWebhookCommand request,
        DateTime now)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = null,
            Action = action,
            EntityType = "PaymentOrder",
            EntityId = order.Id,
            Description = description,
            Metadata =
                $"eventId={request.WebhookEventId}; event={request.PaymentEventName}; " +
                $"paymentId={request.RazorpayPaymentId}",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        });
    }

    private static CompletePaymentFromWebhookResponseDto Result(
        CompletePaymentFromWebhookCommand request,
        PaymentOrder? order,
        string outcome) =>
        new(
            order?.Id,
            order?.RazorpayOrderId ?? request.RazorpayOrderId,
            order?.Status,
            order?.PaidAt,
            request.WebhookEventId,
            Completed: outcome == WebhookOutcomes.Completed,
            outcome);

    /// <summary>
    /// True for the unique violation on <c>webhook_events.EventId</c>
    /// (SQLSTATE 23505). The application layer has no Npgsql reference, so
    /// the code and index name are read from the message, as elsewhere.
    /// </summary>
    private static bool IsDuplicateEventViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException?.Message ?? ex.Message;

        return inner.Contains("23505") &&
               inner.Contains("IX_webhook_events_EventId");
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    /// <summary>The locked invoice, whichever table it lives in.</summary>
    private sealed class LockedInvoice
    {
        private readonly Action<DateTime> _markPaid;

        public LockedInvoice(
            string number,
            string status,
            bool isPending,
            Action<DateTime> markPaid)
        {
            Number = number;
            Status = status;
            IsPending = isPending;
            _markPaid = markPaid;
        }

        public string Number { get; }

        public string Status { get; }

        public bool IsPending { get; }

        public void MarkPaid(DateTime paidAt) => _markPaid(paidAt);
    }
}
