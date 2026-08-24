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

        decimal amount;
        string currency;

        if (request.InvoiceType ==
            PaymentInvoiceType.RetainerInvoice)
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

            if (!_currentUser.IsAdmin &&
                invoice.ClientCompanyId !=
                    _currentUser.ClientCompanyId)
            {
                throw new ForbiddenAccessException(
                    "You do not have access to this invoice.");
            }

            if (invoice.Status !=
                RetainerInvoiceStatus.Pending)
            {
                throw new ConflictException(
                    $"Invoice is {invoice.Status}. Only pending invoices can be paid.");
            }

            var existingOrder =
                await _context.PaymentOrders
                    .FirstOrDefaultAsync(
                        x =>
                            x.InvoiceType ==
                                PaymentInvoiceType
                                    .RetainerInvoice &&
                            x.RetainerInvoiceId ==
                                request.RetainerInvoiceId &&
                            !x.IsDeleted,
                        cancellationToken);

            if (existingOrder is not null &&
                existingOrder.RazorpayOrderId is not null)
            {
                throw new ConflictException(
                    "A payment order already exists for this invoice.");
            }

            amount = invoice.Amount;
            currency = invoice.Currency;

            if (existingOrder is not null)
            {
                var retryReceipt =
                    existingOrder.Id
                        .ToString("N");

                var retryResult =
                    await _razorpayService
                        .CreateOrderAsync(
                            (int)(amount * 100),
                            currency,
                            retryReceipt,
                            cancellationToken);

                existingOrder.RazorpayOrderId =
                    retryResult.OrderId;
                existingOrder.Status =
                    PaymentStatus.Pending;
                existingOrder.UpdatedAt =
                    DateTime.UtcNow;

                await _context.SaveChangesAsync(
                    cancellationToken);

                var razorpayKeyId =
                    _razorpayService.KeyId;

                return
                    new CreatePaymentOrderResponseDto(
                        existingOrder.Id,
                        existingOrder.InvoiceType,
                        existingOrder.RetainerInvoiceId,
                        existingOrder.ServiceFeeInvoiceId,
                        existingOrder.ClientCompanyId,
                        existingOrder.Amount,
                        existingOrder.Currency,
                        existingOrder.Status,
                        existingOrder.RazorpayOrderId,
                        (int)(existingOrder.Amount * 100),
                        razorpayKeyId,
                        existingOrder.CreatedAt);
            }

            var now = DateTime.UtcNow;

            var paymentOrder = new PaymentOrder
            {
                Id = Guid.NewGuid(),
                InvoiceType =
                    PaymentInvoiceType.RetainerInvoice,
                RetainerInvoiceId =
                    request.RetainerInvoiceId,
                ServiceFeeInvoiceId = null,
                ClientCompanyId = invoice.ClientCompanyId,
                Amount = amount,
                Currency = currency,
                Status = PaymentStatus.Created,
                RazorpayOrderId = null,
                RazorpayPaymentId = null,
                RazorpaySignature = null,
                Notes =
                    request.Notes?.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            };

            _context.PaymentOrders.Add(paymentOrder);

            var auditLog1 = new Domain.Entities.AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = _currentUser.UserId,
                Action = "PaymentOrderCreated",
                EntityType = "PaymentOrder",
                EntityId = paymentOrder.Id,
                Description =
                    $"Payment order was created for retainer invoice {request.RetainerInvoiceId}.",
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            };
            _context.AuditLogs.Add(auditLog1);

            await _context.SaveChangesAsync(
                cancellationToken);

            var receipt =
                paymentOrder.Id
                    .ToString("N");

            var razorpayOrder =
                await _razorpayService
                    .CreateOrderAsync(
                        (int)(amount * 100),
                        currency,
                        receipt,
                        cancellationToken);

            paymentOrder.RazorpayOrderId =
                razorpayOrder.OrderId;
            paymentOrder.Status =
                PaymentStatus.Pending;
            paymentOrder.UpdatedAt =
                DateTime.UtcNow;

            await _context.SaveChangesAsync(
                cancellationToken);

            var keyId =
                _razorpayService.KeyId;

            return
                new CreatePaymentOrderResponseDto(
                    paymentOrder.Id,
                    paymentOrder.InvoiceType,
                    paymentOrder.RetainerInvoiceId,
                    paymentOrder.ServiceFeeInvoiceId,
                    paymentOrder.ClientCompanyId,
                    paymentOrder.Amount,
                    paymentOrder.Currency,
                    paymentOrder.Status,
                    paymentOrder.RazorpayOrderId,
                    (int)(paymentOrder.Amount * 100),
                    keyId,
                    paymentOrder.CreatedAt);
        }
        else
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
                                request
                                    .ServiceFeeInvoiceId
                                    .Value &&
                            !x.IsDeleted,
                        cancellationToken);

            if (invoice is null)
            {
                throw new NotFoundException(
                    "Service fee invoice was not found.");
            }

            if (!_currentUser.IsAdmin &&
                invoice.ClientCompanyId !=
                    _currentUser.ClientCompanyId)
            {
                throw new ForbiddenAccessException(
                    "You do not have access to this invoice.");
            }

            if (invoice.Status !=
                ServiceFeeInvoiceStatus.Pending)
            {
                throw new ConflictException(
                    $"Invoice is {invoice.Status}. Only pending invoices can be paid.");
            }

            var existingOrder =
                await _context.PaymentOrders
                    .FirstOrDefaultAsync(
                        x =>
                            x.InvoiceType ==
                                PaymentInvoiceType
                                    .ServiceFeeInvoice &&
                            x.ServiceFeeInvoiceId ==
                                request
                                    .ServiceFeeInvoiceId &&
                            !x.IsDeleted,
                        cancellationToken);

            if (existingOrder is not null &&
                existingOrder.RazorpayOrderId is not null)
            {
                throw new ConflictException(
                    "A payment order already exists for this invoice.");
            }

            amount = invoice.Amount;
            currency = invoice.Currency;

            if (existingOrder is not null)
            {
                var retryReceipt =
                    existingOrder.Id
                        .ToString("N");

                var retryResult =
                    await _razorpayService
                        .CreateOrderAsync(
                            (int)(amount * 100),
                            currency,
                            retryReceipt,
                            cancellationToken);

                existingOrder.RazorpayOrderId =
                    retryResult.OrderId;
                existingOrder.Status =
                    PaymentStatus.Pending;
                existingOrder.UpdatedAt =
                    DateTime.UtcNow;

                await _context.SaveChangesAsync(
                    cancellationToken);

                var razorpayKeyId =
                    _razorpayService.KeyId;

                return
                    new CreatePaymentOrderResponseDto(
                        existingOrder.Id,
                        existingOrder.InvoiceType,
                        existingOrder.RetainerInvoiceId,
                        existingOrder.ServiceFeeInvoiceId,
                        existingOrder.ClientCompanyId,
                        existingOrder.Amount,
                        existingOrder.Currency,
                        existingOrder.Status,
                        existingOrder.RazorpayOrderId,
                        (int)(existingOrder.Amount * 100),
                        razorpayKeyId,
                        existingOrder.CreatedAt);
            }

            var now = DateTime.UtcNow;

            var paymentOrder = new PaymentOrder
            {
                Id = Guid.NewGuid(),
                InvoiceType =
                    PaymentInvoiceType
                        .ServiceFeeInvoice,
                RetainerInvoiceId = null,
                ServiceFeeInvoiceId =
                    request.ServiceFeeInvoiceId,
                ClientCompanyId = invoice.ClientCompanyId,
                Amount = amount,
                Currency = currency,
                Status = PaymentStatus.Created,
                RazorpayOrderId = null,
                RazorpayPaymentId = null,
                RazorpaySignature = null,
                Notes =
                    request.Notes?.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            };

            _context.PaymentOrders.Add(paymentOrder);

            var auditLog2 = new Domain.Entities.AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = _currentUser.UserId,
                Action = "PaymentOrderCreated",
                EntityType = "PaymentOrder",
                EntityId = paymentOrder.Id,
                Description =
                    $"Payment order was created for service fee invoice {request.ServiceFeeInvoiceId}.",
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            };
            _context.AuditLogs.Add(auditLog2);

            await _context.SaveChangesAsync(
                cancellationToken);

            var receipt =
                paymentOrder.Id
                    .ToString("N");

            var razorpayOrder =
                await _razorpayService
                    .CreateOrderAsync(
                        (int)(amount * 100),
                        currency,
                        receipt,
                        cancellationToken);

            paymentOrder.RazorpayOrderId =
                razorpayOrder.OrderId;
            paymentOrder.Status =
                PaymentStatus.Pending;
            paymentOrder.UpdatedAt =
                DateTime.UtcNow;

            await _context.SaveChangesAsync(
                cancellationToken);

            var keyId =
                _razorpayService.KeyId;

            return
                new CreatePaymentOrderResponseDto(
                    paymentOrder.Id,
                    paymentOrder.InvoiceType,
                    paymentOrder.RetainerInvoiceId,
                    paymentOrder.ServiceFeeInvoiceId,
                    paymentOrder.ClientCompanyId,
                    paymentOrder.Amount,
                    paymentOrder.Currency,
                    paymentOrder.Status,
                    paymentOrder.RazorpayOrderId,
                    (int)(paymentOrder.Amount * 100),
                    keyId,
                    paymentOrder.CreatedAt);
        }
    }
}
