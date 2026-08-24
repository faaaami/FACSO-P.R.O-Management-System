using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Commands.VoidServiceFeeInvoice;

public sealed class VoidServiceFeeInvoiceCommandHandler
    : IRequestHandler<
        VoidServiceFeeInvoiceCommand,
        VoidServiceFeeInvoiceResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public VoidServiceFeeInvoiceCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<VoidServiceFeeInvoiceResponseDto> Handle(
        VoidServiceFeeInvoiceCommand request,
        CancellationToken cancellationToken)
    {
        var invoice =
            await _context.ServiceFeeInvoices
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == request.Id &&
                        !x.IsDeleted,
                    cancellationToken);

        if (invoice is null)
        {
            throw new NotFoundException(
                "Service fee invoice was not found.");
        }

        if (invoice.Status !=
            ServiceFeeInvoiceStatus.Pending)
        {
            throw new ConflictException(
                "Only pending invoices can be voided.");
        }

        var now = DateTime.UtcNow;

        invoice.Status =
            ServiceFeeInvoiceStatus.Void;

        invoice.VoidedAt =
            now;

        invoice.VoidReason =
            request.Reason?.Trim();

        invoice.UpdatedAt =
            now;

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ServiceFeeInvoiceVoided",
            EntityType = "ServiceFeeInvoice",
            EntityId = invoice.Id,
            Description =
                $"Service fee invoice '{invoice.InvoiceNumber}' was voided.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };

        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new VoidServiceFeeInvoiceResponseDto(
            invoice.Id,
            invoice.ClientCompanyId,
            invoice.RenewalTaskId,
            invoice.InvoiceNumber,
            invoice.InvoiceDate,
            invoice.DueDate,
            invoice.Amount,
            invoice.Currency,
            invoice.Status,
            invoice.Description,
            invoice.PaidAt,
            invoice.VoidedAt,
            invoice.VoidReason,
            invoice.CreatedAt,
            invoice.UpdatedAt);
    }
}
