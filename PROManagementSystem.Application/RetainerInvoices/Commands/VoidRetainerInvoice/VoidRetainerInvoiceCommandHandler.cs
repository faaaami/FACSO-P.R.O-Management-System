using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.RetainerInvoices.Commands.VoidRetainerInvoice;

public sealed class VoidRetainerInvoiceCommandHandler
    : IRequestHandler<
        VoidRetainerInvoiceCommand,
        VoidRetainerInvoiceResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public VoidRetainerInvoiceCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<VoidRetainerInvoiceResponseDto> Handle(
        VoidRetainerInvoiceCommand request,
        CancellationToken cancellationToken)
    {
        var invoice =
            await _context.RetainerInvoices
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == request.Id &&
                        !x.IsDeleted,
                    cancellationToken);

        if (invoice is null)
        {
            throw new NotFoundException(
                "Retainer invoice was not found.");
        }

        if (invoice.Status !=
            RetainerInvoiceStatus.Pending)
        {
            throw new ConflictException(
                "Only pending invoices can be voided.");
        }

        var now = DateTime.UtcNow;

        invoice.Status =
            RetainerInvoiceStatus.Void;

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
            Action = "RetainerInvoiceVoided",
            EntityType = "RetainerInvoice",
            EntityId = invoice.Id,
            Description =
                $"Retainer invoice '{invoice.InvoiceNumber}' was voided.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };

        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new VoidRetainerInvoiceResponseDto(
            invoice.Id,
            invoice.ClientCompanyId,
            invoice.ServiceContractId,
            invoice.InvoiceNumber,
            invoice.InvoiceDate,
            invoice.DueDate,
            invoice.PeriodStart,
            invoice.PeriodEnd,
            invoice.Amount,
            invoice.Currency,
            invoice.Status,
            invoice.Notes,
            invoice.PaidAt,
            invoice.VoidedAt,
            invoice.VoidReason,
            invoice.CreatedAt,
            invoice.UpdatedAt);
    }
}
