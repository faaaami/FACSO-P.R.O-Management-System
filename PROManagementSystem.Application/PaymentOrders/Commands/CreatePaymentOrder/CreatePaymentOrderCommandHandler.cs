using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.PaymentOrders.Commands.CreatePaymentOrder;

public sealed class CreatePaymentOrderCommandHandler
    : IRequestHandler<
        CreatePaymentOrderCommand,
        CreatePaymentOrderResponseDto>
{
    /// <summary>
    /// Statuses in which an existing PaymentOrder is still a live, resumable
    /// checkout ("open"). An invoice may carry any number of closed orders
    /// (Paid, Failed, Cancelled, Refunded, PaidRequiresReview) but at most one
    /// open order, enforced by the filtered unique indexes
    /// <c>IX_payment_orders_*InvoiceId_Open</c>. A closed order is never
    /// resurrected or repurposed; a pending invoice whose orders are all
    /// closed simply gets a new one.
    /// </summary>
    private static readonly HashSet<PaymentStatus> ReusableStatuses =
        new()
        {
            PaymentStatus.Created,
            PaymentStatus.Pending
        };

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRazorpayService _razorpayService;

    public CreatePaymentOrderCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IRazorpayService razorpayService)
    {
        _context = context;
        _currentUser = currentUser;
        _razorpayService = razorpayService;
    }

    public async Task<CreatePaymentOrderResponseDto> Handle(
        CreatePaymentOrderCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin && !_currentUser.ClientCompanyId.HasValue)
        {
            throw new ForbiddenAccessException(
                "Client company association is required.");
        }

        return request.InvoiceType ==
                PaymentInvoiceType.RetainerInvoice
            ? await HandleRetainerAsync(request, cancellationToken)
            : await HandleServiceFeeAsync(request, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Invoice-type branches. Eligibility (existence, ownership, Pending)
    // is always resolved BEFORE the existing-order lookup, so reuse can
    // never resurrect an invoice that is not payable or not owned by the
    // caller.
    // ------------------------------------------------------------------

    private async Task<CreatePaymentOrderResponseDto> HandleRetainerAsync(
        CreatePaymentOrderCommand request,
        CancellationToken cancellationToken)
    {
        if (!request.RetainerInvoiceId.HasValue)
        {
            throw new ConflictException(
                "Retainer invoice ID is required.");
        }

        var invoice =
            await _context.RetainerInvoices
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            request.RetainerInvoiceId.Value &&
                        !x.IsDeleted,
                    cancellationToken);

        if (invoice is null)
        {
            throw new NotFoundException(
                "Retainer invoice was not found.");
        }

        EnsureInvoiceOwnership(invoice.ClientCompanyId);

        if (invoice.Status != RetainerInvoiceStatus.Pending)
        {
            throw new ConflictException(
                $"Invoice is {invoice.Status}. Only pending invoices can be paid.");
        }

        // Snapshot read: this result only drives the branch decision and is
        // never mutated. It is intentionally untracked so the locked re-read
        // on the retry path can materialise a fresh, current instance.
        var existingOrder =
            await _context.PaymentOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.InvoiceType ==
                            PaymentInvoiceType.RetainerInvoice &&
                        x.RetainerInvoiceId ==
                            request.RetainerInvoiceId &&
                        (x.Status == PaymentStatus.Created ||
                         x.Status == PaymentStatus.Pending) &&
                        !x.IsDeleted,
                    cancellationToken);

        if (existingOrder is not null)
        {
            return await ResolveExistingOrderAsync(
                existingOrder,
                "retainer invoice",
                request.RetainerInvoiceId.Value,
                cancellationToken);
        }

        await EnsureNoSettledOrderAsync(
            PaymentInvoiceType.RetainerInvoice,
            request.RetainerInvoiceId.Value,
            "retainer invoice",
            cancellationToken);

        return await CreateNewOrderAsync(
            new NewOrderDetails(
                PaymentInvoiceType.RetainerInvoice,
                RetainerInvoiceId:
                    request.RetainerInvoiceId.Value,
                ServiceFeeInvoiceId: null,
                ClientCompanyId: invoice.ClientCompanyId,
                Amount: invoice.Amount,
                Currency: invoice.Currency,
                Notes: request.Notes,
                InvoiceKind: "retainer invoice",
                InvoiceId: request.RetainerInvoiceId.Value),
            cancellationToken);
    }

    private async Task<CreatePaymentOrderResponseDto> HandleServiceFeeAsync(
        CreatePaymentOrderCommand request,
        CancellationToken cancellationToken)
    {
        if (!request.ServiceFeeInvoiceId.HasValue)
        {
            throw new ConflictException(
                "Service fee invoice ID is required.");
        }

        var invoice =
            await _context.ServiceFeeInvoices
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            request.ServiceFeeInvoiceId.Value &&
                        !x.IsDeleted,
                    cancellationToken);

        if (invoice is null)
        {
            throw new NotFoundException(
                "Service fee invoice was not found.");
        }

        EnsureInvoiceOwnership(invoice.ClientCompanyId);

        if (invoice.Status != ServiceFeeInvoiceStatus.Pending)
        {
            throw new ConflictException(
                $"Invoice is {invoice.Status}. Only pending invoices can be paid.");
        }

        var existingOrder =
            await _context.PaymentOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.InvoiceType ==
                            PaymentInvoiceType.ServiceFeeInvoice &&
                        x.ServiceFeeInvoiceId ==
                            request.ServiceFeeInvoiceId &&
                        (x.Status == PaymentStatus.Created ||
                         x.Status == PaymentStatus.Pending) &&
                        !x.IsDeleted,
                    cancellationToken);

        if (existingOrder is not null)
        {
            return await ResolveExistingOrderAsync(
                existingOrder,
                "service fee invoice",
                request.ServiceFeeInvoiceId.Value,
                cancellationToken);
        }

        await EnsureNoSettledOrderAsync(
            PaymentInvoiceType.ServiceFeeInvoice,
            request.ServiceFeeInvoiceId.Value,
            "service fee invoice",
            cancellationToken);

        return await CreateNewOrderAsync(
            new NewOrderDetails(
                PaymentInvoiceType.ServiceFeeInvoice,
                RetainerInvoiceId: null,
                ServiceFeeInvoiceId:
                    request.ServiceFeeInvoiceId.Value,
                ClientCompanyId: invoice.ClientCompanyId,
                Amount: invoice.Amount,
                Currency: invoice.Currency,
                Notes: request.Notes,
                InvoiceKind: "service fee invoice",
                InvoiceId: request.ServiceFeeInvoiceId.Value),
            cancellationToken);
    }

    // ------------------------------------------------------------------
    // Existing-order decision
    // ------------------------------------------------------------------

    private async Task<CreatePaymentOrderResponseDto> ResolveExistingOrderAsync(
        PaymentOrder existingOrder,
        string invoiceKind,
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        // Only open orders are looked up, but the snapshot can be stale.
        // A closed order is never resumed or repurposed.
        if (!ReusableStatuses.Contains(existingOrder.Status))
        {
            throw BuildTerminalConflict(
                existingOrder.Status, invoiceKind);
        }

        // A live gateway order already exists: resume that exact order.
        // No Razorpay call, no new row, and no mutation of the order.
        if (existingOrder.RazorpayOrderId is not null)
        {
            return await ReuseExistingOrderAsync(
                existingOrder,
                invoiceKind,
                invoiceId,
                cancellationToken);
        }

        // Created/Pending with no gateway order: the earlier attempt stopped
        // before its gateway call completed. Take the row lock so two
        // concurrent requests cannot each create a gateway order here.
        return await RetryWithoutGatewayOrderAsync(
            existingOrder,
            invoiceKind,
            invoiceId,
            cancellationToken);
    }

    private async Task<CreatePaymentOrderResponseDto> ReuseExistingOrderAsync(
        PaymentOrder existingOrder,
        string invoiceKind,
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        // Audit only. The PaymentOrder is deliberately NOT mutated: status,
        // amount, currency, RazorpayOrderId, CreatedAt and UpdatedAt all
        // stay exactly as persisted.
        RecordAudit(
            "PaymentOrderReused",
            existingOrder.Id,
            $"Existing payment order '{existingOrder.Id}' with Razorpay order " +
            $"'{existingOrder.RazorpayOrderId}' was reused for " +
            $"{invoiceKind} {invoiceId}. No new gateway order was created.",
            DateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return MapToResponse(existingOrder);
    }

    private async Task<CreatePaymentOrderResponseDto> RetryWithoutGatewayOrderAsync(
        PaymentOrder existingOrder,
        string invoiceKind,
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        // The lock is scoped to this null-ID retry path only. It is allowed to
        // span the Razorpay call so that exactly one gateway order can ever be
        // created for this row.
        await using var transaction =
            await _context.BeginTransactionAsync(cancellationToken);

        // Tracked (never AsNoTracking) so the mutation below persists
        // through normal change tracking and EF identity resolution.
        // The pre-lock lookup was untracked, so this materialises a fresh
        // instance carrying current database values.
        var lockedRows =
            await _context.PaymentOrders
                .FromSqlInterpolated(
                    $"SELECT * FROM payment_orders WHERE \"Id\" = {existingOrder.Id} AND \"IsDeleted\" = false FOR UPDATE")
                .ToListAsync(cancellationToken);

        var order = lockedRows.SingleOrDefault();

        if (order is null)
        {
            // The row was soft-deleted while we waited for the lock.
            await transaction.RollbackAsync(cancellationToken);

            throw new NotFoundException(
                "Payment order was not found.");
        }

        // MANDATORY post-lock re-check. A competing request may have already
        // populated the gateway order while this one waited on the lock.
        if (!ReusableStatuses.Contains(order.Status))
        {
            await transaction.RollbackAsync(cancellationToken);

            throw BuildTerminalConflict(
                order.Status, invoiceKind);
        }

        if (order.RazorpayOrderId is not null)
        {
            // The other request won the race. Reuse its gateway order
            // instead of issuing a second one.
            var reused = await ReuseExistingOrderAsync(
                order,
                invoiceKind,
                invoiceId,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return reused;
        }

        var now = DateTime.UtcNow;

        // The gateway order is created from the PERSISTED PaymentOrder
        // amount/currency, never from the invoice, so the Razorpay order
        // amount, the response amount and the amount the webhook validates
        // can never diverge.
        var retryResult =
            await _razorpayService
                .CreateOrderAsync(
                    (int)(order.Amount * 100),
                    order.Currency,
                    order.Id.ToString("N"),
                    cancellationToken);

        order.RazorpayOrderId = retryResult.OrderId;
        order.Status = PaymentStatus.Pending;
        order.UpdatedAt = now;

        RecordAudit(
            "PaymentOrderRetried",
            order.Id,
            $"Payment order was retried for {invoiceKind} {invoiceId}.",
            now);

        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return MapToResponse(order);
    }

    // ------------------------------------------------------------------
    // New order
    // ------------------------------------------------------------------

    private async Task<CreatePaymentOrderResponseDto> CreateNewOrderAsync(
        NewOrderDetails details,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var paymentOrder = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            InvoiceType = details.InvoiceType,
            RetainerInvoiceId = details.RetainerInvoiceId,
            ServiceFeeInvoiceId = details.ServiceFeeInvoiceId,
            ClientCompanyId = details.ClientCompanyId,
            Amount = details.Amount,
            Currency = details.Currency,
            Status = PaymentStatus.Created,
            RazorpayOrderId = null,
            RazorpayPaymentId = null,
            RazorpaySignature = null,
            Notes = details.Notes?.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };

        _context.PaymentOrders.Add(paymentOrder);

        RecordAudit(
            "PaymentOrderCreated",
            paymentOrder.Id,
            $"Payment order was created for " +
            $"{details.InvoiceKind} {details.InvoiceId}.",
            now);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            IsPaymentOrderUniqueViolation(ex))
        {
            // A concurrent request inserted this invoice's open order between
            // our lookup and this insert. The filtered unique index is the
            // database-level backstop; adopt the winning row instead of
            // surfacing a 500. Any other database error is not caught here
            // and propagates unchanged.
            DetachFailedInserts();

            var winner =
                await _context.PaymentOrders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.InvoiceType ==
                                details.InvoiceType &&
                            x.RetainerInvoiceId ==
                                details.RetainerInvoiceId &&
                            x.ServiceFeeInvoiceId ==
                                details.ServiceFeeInvoiceId &&
                            (x.Status == PaymentStatus.Created ||
                             x.Status == PaymentStatus.Pending) &&
                            !x.IsDeleted,
                        cancellationToken);

            if (winner is null)
            {
                throw;
            }

            // A winner that already holds a gateway order is safe to adopt
            // and reuse. A winner that has not obtained one yet is reported
            // as a conflict rather than retried, so this path can never
            // create a second gateway order for the same invoice.
            if (winner.RazorpayOrderId is null)
            {
                throw new ConflictException(
                    "A payment order for this invoice is already being " +
                    "created. Please retry.");
            }

            return await ResolveExistingOrderAsync(
                winner,
                details.InvoiceKind,
                details.InvoiceId,
                cancellationToken);
        }

        var razorpayOrder =
            await _razorpayService
                .CreateOrderAsync(
                    (int)(paymentOrder.Amount * 100),
                    paymentOrder.Currency,
                    paymentOrder.Id.ToString("N"),
                    cancellationToken);

        // Conditional claim: the order was committed as Created before the
        // gateway call, so a manual mark-paid or void may have cancelled it
        // in the meantime. Never overwrite that cancellation with Pending.
        var pendingAt = DateTime.UtcNow;

        var claimed =
            await _context.PaymentOrders
                .Where(x =>
                    x.Id == paymentOrder.Id &&
                    x.Status == PaymentStatus.Created)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.RazorpayOrderId, razorpayOrder.OrderId)
                        .SetProperty(x => x.Status, PaymentStatus.Pending)
                        .SetProperty(x => x.UpdatedAt, pendingAt),
                    cancellationToken);

        if (claimed == 0)
        {
            // The new gateway order is left unreferenced (Razorpay cannot
            // close it). It was never returned to the client, and a capture
            // on it would arrive as an unknown order and notify admins.
            throw new ConflictException(
                $"The {details.InvoiceKind} changed while the payment was " +
                "being prepared. Please refresh and try again.");
        }

        paymentOrder.RazorpayOrderId = razorpayOrder.OrderId;
        paymentOrder.Status = PaymentStatus.Pending;
        paymentOrder.UpdatedAt = pendingAt;

        return MapToResponse(paymentOrder);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Money already captured under a still-Pending invoice - a Paid order
    /// (the records drifted apart) or a PaidRequiresReview order (e.g. an
    /// amount or currency mismatch) - means another checkout could charge
    /// the client twice, so the invoice must be reconciled by an admin first.
    /// </summary>
    private async Task EnsureNoSettledOrderAsync(
        PaymentInvoiceType invoiceType,
        Guid invoiceId,
        string invoiceKind,
        CancellationToken cancellationToken)
    {
        var capturedStatus =
            await _context.PaymentOrders
                .AsNoTracking()
                .Where(
                    x =>
                        x.InvoiceType == invoiceType &&
                        (invoiceType == PaymentInvoiceType.RetainerInvoice
                            ? x.RetainerInvoiceId == invoiceId
                            : x.ServiceFeeInvoiceId == invoiceId) &&
                        (x.Status == PaymentStatus.Paid ||
                         x.Status == PaymentStatus.PaidRequiresReview) &&
                        !x.IsDeleted)
                .Select(x => (PaymentStatus?)x.Status)
                .FirstOrDefaultAsync(cancellationToken);

        if (capturedStatus == PaymentStatus.PaidRequiresReview)
        {
            throw new ConflictException(
                $"A payment for this {invoiceKind} was received and is " +
                "awaiting review by the PRO team. A new payment cannot be " +
                "taken until it is resolved.");
        }

        if (capturedStatus == PaymentStatus.Paid)
        {
            throw new ConflictException(
                $"A completed payment already exists for this {invoiceKind}. " +
                "An admin must reconcile the invoice before a new payment " +
                "can be taken.");
        }
    }

    private void EnsureInvoiceOwnership(Guid clientCompanyId)
    {
        if (!_currentUser.IsAdmin &&
            clientCompanyId != _currentUser.ClientCompanyId)
        {
            throw new ForbiddenAccessException(
                "You do not have access to this invoice.");
        }
    }

    private void RecordAudit(
        string action,
        Guid entityId,
        string description,
        DateTime now)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = action,
            EntityType = "PaymentOrder",
            EntityId = entityId,
            Description = description,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        });
    }

    /// <summary>
    /// Single mapping point for every return path, so a freshly created, a
    /// reused and a retried order all project the same persisted fields.
    /// <c>RazorpayAmount</c> is always derived from the PERSISTED order
    /// amount - never from the invoice and never from the request.
    /// </summary>
    private CreatePaymentOrderResponseDto MapToResponse(
        PaymentOrder order)
    {
        return new CreatePaymentOrderResponseDto(
            order.Id,
            order.InvoiceType,
            order.RetainerInvoiceId,
            order.ServiceFeeInvoiceId,
            order.ClientCompanyId,
            order.Amount,
            order.Currency,
            order.Status,
            order.RazorpayOrderId,
            (int)(order.Amount * 100),
            _razorpayService.KeyId,
            order.CreatedAt);
    }

    private static ConflictException BuildTerminalConflict(
        PaymentStatus status,
        string invoiceKind)
    {
        return new ConflictException(
            $"The payment order for this {invoiceKind} changed to " +
            $"{status} while it was being prepared. Please retry.");
    }

    /// <summary>
    /// Detaches the entities left in the Added state by a failed
    /// SaveChanges so the context is usable again. The application context
    /// exposes DbSets rather than the ChangeTracker, so the tracked set is
    /// reached through each DbSet's Local view.
    /// </summary>
    private void DetachFailedInserts()
    {
        foreach (var tracked in
                 _context.PaymentOrders.Local.ToList())
        {
            _context.PaymentOrders.Remove(tracked);
        }

        foreach (var tracked in _context.AuditLogs.Local.ToList())
        {
            _context.AuditLogs.Remove(tracked);
        }
    }

    /// <summary>
    /// Returns <c>true</c> when the <see cref="DbUpdateException"/> is the
    /// filtered-unique one-open-payment-order-per-invoice violation
    /// (PostgreSQL error code 23505) and nothing else.
    /// </summary>
    private static bool IsPaymentOrderUniqueViolation(
        DbUpdateException ex)
    {
        // The application layer has no Npgsql reference, so - matching the
        // existing RenewalTaskCreationService pattern - the SQLSTATE and
        // constraint name are read from the message. Npgsql wraps the
        // PostgresException as the inner exception.
        var inner = ex.InnerException?.Message ?? ex.Message;

        return inner.Contains("23505") &&
               (inner.Contains("IX_payment_orders_RetainerInvoiceId_Open") ||
                inner.Contains("IX_payment_orders_ServiceFeeInvoiceId_Open"));
    }

    private sealed record NewOrderDetails(
        PaymentInvoiceType InvoiceType,
        Guid? RetainerInvoiceId,
        Guid? ServiceFeeInvoiceId,
        Guid ClientCompanyId,
        decimal Amount,
        string Currency,
        string? Notes,
        string InvoiceKind,
        Guid InvoiceId);
}
