using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Services;

public sealed class ServiceFeeInvoiceCreationService
    : IServiceFeeInvoiceCreationService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ServiceFeeInvoiceCreationService(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ServiceFeeInvoice> CreateAsync(
        Guid clientCompanyId,
        Guid renewalTaskId,
        decimal amount,
        string currency,
        DateOnly invoiceDate,
        DateOnly dueDate,
        string invoiceNumber,
        string? description,
        CancellationToken cancellationToken)
    {
        // 1. Verify client company exists and is not deleted
        var companyExists =
            await _context.ClientCompanies
                .AnyAsync(
                    x =>
                        x.Id == clientCompanyId &&
                        !x.IsDeleted,
                    cancellationToken);

        if (!companyExists)
        {
            throw new NotFoundException(
                "Client company was not found.");
        }

        // 2. Verify renewal task exists and is not deleted
        var task =
            await _context.RenewalTasks
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == renewalTaskId &&
                        !x.IsDeleted,
                    cancellationToken);

        if (task is null)
        {
            throw new NotFoundException(
                "Renewal task was not found.");
        }

        // 3. Verify renewal task belongs to the client company
        if (task.ClientCompanyId != clientCompanyId)
        {
            throw new ConflictException(
                "The renewal task does not belong to the specified client company.");
        }

        // 4. Verify renewal task is in terminal completed state
        if (task.Status != RenewalTaskStatus.Updated)
        {
            throw new ConflictException(
                "The renewal task must be completed (Updated) to create a service fee invoice.");
        }

        // 5. Check invoice number uniqueness among non-deleted
        var invoiceNumberExists =
            await _context.ServiceFeeInvoices
                .AnyAsync(
                    x =>
                        x.InvoiceNumber ==
                            invoiceNumber.Trim() &&
                        !x.IsDeleted,
                    cancellationToken);

        if (invoiceNumberExists)
        {
            throw new ConflictException(
                "A service fee invoice with this invoice number already exists.");
        }

        // 6. Check one active invoice per renewal task
        var existingInvoiceForTask =
            await _context.ServiceFeeInvoices
                .AnyAsync(
                    x =>
                        x.RenewalTaskId == renewalTaskId &&
                        !x.IsDeleted,
                    cancellationToken);

        if (existingInvoiceForTask)
        {
            throw new ConflictException(
                "A service fee invoice already exists for this renewal task.");
        }

        // 7. Create the invoice
        var now = DateTime.UtcNow;

        var invoice = new ServiceFeeInvoice
        {
            Id = Guid.NewGuid(),

            ClientCompanyId =
                clientCompanyId,

            RenewalTaskId =
                renewalTaskId,

            InvoiceNumber =
                invoiceNumber.Trim(),

            InvoiceDate =
                invoiceDate,

            DueDate =
                dueDate,

            Amount =
                amount,

            Currency =
                currency.Trim(),

            Status =
                ServiceFeeInvoiceStatus.Pending,

            Description =
                description?.Trim(),

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

        _context.ServiceFeeInvoices.Add(invoice);

        // 8. Record creation (covers both the API and the
        //    renewal task completed event driven paths)
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ServiceFeeInvoiceCreated",
            EntityType = "ServiceFeeInvoice",
            EntityId = invoice.Id,
            Description =
                $"Service fee invoice '{invoice.InvoiceNumber}' was created " +
                $"for renewal task '{renewalTaskId}'.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return invoice;
    }
}
