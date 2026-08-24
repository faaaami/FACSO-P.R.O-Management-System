using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceFeeInvoices.Commands.MarkServiceFeeInvoicePaid;

public sealed class MarkServiceFeeInvoicePaidCommandHandler
    : IRequestHandler<
        MarkServiceFeeInvoicePaidCommand,
        MarkServiceFeeInvoicePaidResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public MarkServiceFeeInvoicePaidCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<MarkServiceFeeInvoicePaidResponseDto> Handle(
        MarkServiceFeeInvoicePaidCommand request,
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
                "Only pending invoices can be marked as paid.");
        }

        var now = DateTime.UtcNow;

        invoice.Status =
            ServiceFeeInvoiceStatus.Paid;

        invoice.PaidAt =
            now;

        invoice.UpdatedAt =
            now;

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ServiceFeeInvoicePaid",
            EntityType = "ServiceFeeInvoice",
            EntityId = invoice.Id,
            Description =
                $"Service fee invoice '{invoice.InvoiceNumber}' was marked as paid.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };

        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new MarkServiceFeeInvoicePaidResponseDto(
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
            invoice.CreatedAt,
            invoice.UpdatedAt);
    }
}
