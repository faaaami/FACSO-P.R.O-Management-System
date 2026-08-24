using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Commands.CreateRenewalTask;

public sealed class CreateRenewalTaskCommandHandler
    : IRequestHandler<
        CreateRenewalTaskCommand,
        CreateRenewalTaskResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateRenewalTaskCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CreateRenewalTaskResponseDto> Handle(
        CreateRenewalTaskCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Get current authenticated user
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "Authenticated user was not found.");
        }

        var currentUserId = _currentUser.UserId.Value;

        // 2. Verify current user exists
        var currentUserExists =
            await _context.Users
                .AnyAsync(
                    x =>
                        x.Id == currentUserId &&
                        !x.IsDeleted,
                    cancellationToken);

        if (!currentUserExists)
        {
            throw new UnauthorizedAccessException(
                "The authenticated user does not exist.");
        }

        // 3. Find document
        //
        // IgnoreQueryFilters is intentionally used here
        // while we diagnose the document lookup.
        var document =
            await _context.Documents
                .IgnoreQueryFilters()
                .Include(x => x.Employee)
                    .ThenInclude(x => x!.ClientEntity)
                .Include(x => x.ClientEntity)
                .FirstOrDefaultAsync(
                    x => x.Id == request.DocumentId,
                    cancellationToken);

        if (document is null)
        {
            throw new KeyNotFoundException(
                "Document was not found.");
        }

        // 4. Do not allow deleted documents
        if (document.IsDeleted)
        {
            throw new InvalidOperationException(
                "Cannot create a renewal task for a deleted document.");
        }

        // 5. Document must be active
        if (!document.IsActive)
        {
            throw new InvalidOperationException(
                "Cannot create a renewal task for an inactive document.");
        }

        // 6. Determine ClientCompanyId
        Guid? clientCompanyId = null;

        // Entity-owned document
        if (document.ClientEntityId.HasValue)
        {
            clientCompanyId =
                document.ClientEntity?.ClientCompanyId;
        }

        // Employee-owned document
        else if (document.EmployeeId.HasValue)
        {
            clientCompanyId =
                document.Employee?
                    .ClientEntity?
                    .ClientCompanyId;
        }

        // 7. Validate company
        if (!clientCompanyId.HasValue)
        {
            throw new InvalidOperationException(
                "Unable to determine the client company for this document.");
        }

        // 8. Prevent duplicate active task
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
            throw new InvalidOperationException(
                "An active renewal task already exists for this document.");
        }

        // 9. Create task
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

        // 10. Create initial history
        var history = new RenewalTaskHistory
        {
            Id = Guid.NewGuid(),

            RenewalTaskId =
                task.Id,

            Status =
                RenewalTaskStatus.Submitted,

            ChangedBy =
                currentUserId,

            ChangedAt =
                now,

            Note =
                "Renewal task created.",

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

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "RenewalTaskCreated",
            EntityType = "RenewalTask",
            EntityId = task.Id,
            Description =
                $"Renewal task was created for document '{document.DocumentNumber}'.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        // 11. Save
        await _context.SaveChangesAsync(
            cancellationToken);

        // 12. Response
        return new CreateRenewalTaskResponseDto(
            task.Id,
            task.DocumentId,
            task.ClientCompanyId,
            task.AssignedStaffId,
            task.Status,
            task.BlockedReason,
            task.BlockedSince,
            task.CompletedAt,
            task.CreatedAt);
    }
}