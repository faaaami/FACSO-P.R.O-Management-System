using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Services;

public sealed class RenewalTaskCreationService
    : IRenewalTaskCreationService
{
    private readonly IApplicationDbContext _context;

    public RenewalTaskCreationService(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid?> CreateAsync(
        Guid documentId,
        Guid changedBy,
        CancellationToken cancellationToken)
    {
        // 1. Verify actor exists
        var userExists =
            await _context.Users
                .AnyAsync(
                    x =>
                        x.Id == changedBy &&
                        !x.IsDeleted,
                    cancellationToken);

        if (!userExists)
        {
            throw new KeyNotFoundException(
                "The user creating the renewal task was not found.");
        }

        // 2. Find document
        var document =
            await _context.Documents
                .IgnoreQueryFilters()
                .Include(x => x.Employee)
                    .ThenInclude(x => x!.ClientEntity)
                .Include(x => x.ClientEntity)
                .FirstOrDefaultAsync(
                    x => x.Id == documentId,
                    cancellationToken);

        if (document is null)
        {
            throw new KeyNotFoundException(
                "Document was not found.");
        }

        // 3. Don't allow deleted documents
        if (document.IsDeleted)
        {
            throw new InvalidOperationException(
                "Cannot create a renewal task for a deleted document.");
        }

        // 4. Document must be active
        if (!document.IsActive)
        {
            throw new InvalidOperationException(
                "Cannot create a renewal task for an inactive document.");
        }

        // 5. Determine ClientCompanyId
        Guid? clientCompanyId = null;

        if (document.ClientEntityId.HasValue)
        {
            clientCompanyId =
                document.ClientEntity?.ClientCompanyId;
        }
        else if (document.EmployeeId.HasValue)
        {
            clientCompanyId =
                document.Employee?
                    .ClientEntity?
                    .ClientCompanyId;
        }

        // 6. Validate company
        if (!clientCompanyId.HasValue)
        {
            throw new InvalidOperationException(
                "Unable to determine the client company for this document.");
        }

        // 7. Prevent duplicate active task
        var existingTask =
            await _context.RenewalTasks
                .AnyAsync(
                    x =>
                        x.DocumentId == document.Id &&
                        !x.IsDeleted &&
                        x.Status != RenewalTaskStatus.Updated,
                    cancellationToken);

        if (existingTask)
        {
            return null;
        }

        // 8. Create task
        var now = DateTime.UtcNow;

        var task = new RenewalTask
        {
            Id = Guid.NewGuid(),

            DocumentId =
                document.Id,

            ClientCompanyId =
                clientCompanyId.Value,

            AssignedStaffId =
                null,

            Status =
                RenewalTaskStatus.Submitted,

            BlockedReason =
                null,

            BlockedSince =
                null,

            CompletedAt =
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

        _context.RenewalTasks.Add(task);

        // 9. Create history
        var history = new RenewalTaskHistory
        {
            Id = Guid.NewGuid(),

            RenewalTaskId =
                task.Id,

            Status =
                RenewalTaskStatus.Submitted,

            ChangedBy =
                changedBy,

            ChangedAt =
                now,

            Note =
                "Renewal task created automatically.",

            CreatedAt =
                now,

            UpdatedAt =
                now,

            IsDeleted =
                false,

            DeletedAt =
                null
        };

        _context.RenewalTaskHistories.Add(history);

        // 10. Save
        await _context.SaveChangesAsync(
            cancellationToken);

        return task.Id;
    }
}