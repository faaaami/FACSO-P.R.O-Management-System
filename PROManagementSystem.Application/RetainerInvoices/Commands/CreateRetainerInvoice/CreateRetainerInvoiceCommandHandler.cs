using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Notifications;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.RetainerInvoices.Commands.CreateRetainerInvoice;

public sealed class CreateRetainerInvoiceCommandHandler
    : IRequestHandler<
        CreateRetainerInvoiceCommand,
        CreateRetainerInvoiceResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserNotifier _notifier;

    public CreateRetainerInvoiceCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IUserNotifier notifier)
    {
        _context = context;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<CreateRetainerInvoiceResponseDto> Handle(
        CreateRetainerInvoiceCommand request,
        CancellationToken cancellationToken)
    {
        var companyExists =
            await _context.ClientCompanies
                .AnyAsync(
                    x =>
                        x.Id == request.ClientCompanyId &&
                        !x.IsDeleted,
                    cancellationToken);

        if (!companyExists)
        {
            throw new NotFoundException(
                "Client company was not found.");
        }

        var contract =
            await _context.ServiceContracts
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == request.ServiceContractId &&
                        !x.IsDeleted,
                    cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException(
                "Service contract was not found.");
        }

        if (contract.ClientCompanyId !=
            request.ClientCompanyId)
        {
            throw new ConflictException(
                "The service contract does not belong to the specified client company.");
        }

        if (contract.Status !=
            ServiceContractStatus.Active)
        {
            throw new ConflictException(
                "The service contract must be active to create a retainer invoice.");
        }

        var invoiceNumberExists =
            await _context.RetainerInvoices
                .AnyAsync(
                    x =>
                        x.InvoiceNumber ==
                            request.InvoiceNumber.Trim() &&
                        !x.IsDeleted,
                    cancellationToken);

        if (invoiceNumberExists)
        {
            throw new ConflictException(
                "A retainer invoice with this invoice number already exists.");
        }

        var now = DateTime.UtcNow;

        var invoice = new RetainerInvoice
        {
            Id = Guid.NewGuid(),

            ClientCompanyId =
                request.ClientCompanyId,

            ServiceContractId =
                request.ServiceContractId,

            InvoiceNumber =
                request.InvoiceNumber.Trim(),

            InvoiceDate =
                request.InvoiceDate,

            DueDate =
                request.DueDate,

            PeriodStart =
                request.PeriodStart,

            PeriodEnd =
                request.PeriodEnd,

            Amount =
                request.Amount,

            Currency =
                request.Currency.Trim(),

            Status =
                RetainerInvoiceStatus.Pending,

            Notes =
                request.Notes?.Trim(),

            PaidAt =
                null,

            VoidedAt =
                null,

            VoidReason =
                null,

            CreatedAt =
                now,

            UpdatedAt =
                now,

            IsDeleted =
                false,

            DeletedAt =
                null
        };

        _context.RetainerInvoices.Add(invoice);

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "RetainerInvoiceCreated",
            EntityType = "RetainerInvoice",
            EntityId = invoice.Id,
            Description =
                $"Retainer invoice '{invoice.InvoiceNumber}' was created for {invoice.Amount} {invoice.Currency}.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };

        _context.AuditLogs.Add(auditLog);

        // The client is told the invoice is ready to pay. The notifier saves
        // the invoice, the audit row and the notifications in one write.
        await _notifier.NotifyClientContactsAsync(
            invoice.ClientCompanyId,
            "New invoice",
            NotificationLabels.NewInvoice(
                invoice.InvoiceNumber,
                invoice.Amount,
                invoice.Currency,
                invoice.DueDate),
            cancellationToken);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new CreateRetainerInvoiceResponseDto(
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
            invoice.CreatedAt);
    }
}
