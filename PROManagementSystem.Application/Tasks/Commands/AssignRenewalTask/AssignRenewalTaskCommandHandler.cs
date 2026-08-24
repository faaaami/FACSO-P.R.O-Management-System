using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Realtime;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Commands.AssignRenewalTask;

public sealed class AssignRenewalTaskCommandHandler
    : IRequestHandler<
        AssignRenewalTaskCommand,
        AssignRenewalTaskResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRealtimeNotificationService _realtime;

    public AssignRenewalTaskCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IRealtimeNotificationService realtime)
    {
        _context = context;
        _currentUser = currentUser;
        _realtime = realtime;
    }

    public async Task<AssignRenewalTaskResponseDto> Handle(
        AssignRenewalTaskCommand request,
        CancellationToken cancellationToken)
    {
        // =========================================================
        // 1. Validate authenticated user
        // =========================================================

        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "Authenticated user was not found.");
        }

        var currentUserId = _currentUser.UserId.Value;


        // =========================================================
        // 2. Find renewal task
        // =========================================================

        var task =
            await _context.RenewalTasks
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == request.TaskId &&
                        !x.IsDeleted,
                    cancellationToken);

        if (task is null)
        {
            throw new KeyNotFoundException(
                "Renewal task was not found.");
        }


        // =========================================================
        // 3. Don't allow assignment of completed task
        // =========================================================

        if (task.Status == RenewalTaskStatus.Updated)
        {
            throw new InvalidOperationException(
                "Cannot assign a completed renewal task.");
        }


        // =========================================================
        // 4. Find staff member
        // =========================================================

        var staff =
            await _context.Users
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == request.StaffId &&
                        !x.IsDeleted,
                    cancellationToken);

        if (staff is null)
        {
            throw new KeyNotFoundException(
                "Staff member was not found.");
        }


        // =========================================================
        // 5. Staff must be an Agent
        // =========================================================

        if (staff.Role != UserRole.Agent)
        {
            throw new InvalidOperationException(
                "The selected user is not an Agent.");
        }


        // =========================================================
        // 6. Staff must be active
        // =========================================================

        if (!staff.IsActive)
        {
            throw new InvalidOperationException(
                "The selected Agent is inactive.");
        }


        // =========================================================
        // 7. Find linked document
        // =========================================================

        var document =
            await _context.Documents
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == task.DocumentId &&
                        !x.IsDeleted,
                    cancellationToken);

        if (document is null)
        {
            throw new KeyNotFoundException(
                "The document linked to this task was not found.");
        }


        // =========================================================
        // 8. Assign staff
        // =========================================================

        task.AssignedStaffId = staff.Id;
        task.UpdatedAt = DateTime.UtcNow;


        // =========================================================
        // 9. Create task history
        // =========================================================

        var history = new RenewalTaskHistory
        {
            Id = Guid.NewGuid(),

            RenewalTaskId = task.Id,

            Status = task.Status,

            ChangedBy = currentUserId,

            ChangedAt = task.UpdatedAt,

            Note =
                $"Task assigned to Agent: {staff.FullName}.",

            CreatedAt = task.UpdatedAt,

            UpdatedAt = task.UpdatedAt,

            IsDeleted = false,

            DeletedAt = null
        };

        _context.RenewalTaskHistories.Add(history);

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "RenewalTaskAssigned",
            EntityType = "RenewalTask",
            EntityId = task.Id,
            Description =
                $"Renewal task was assigned to Agent '{staff.FullName}'.",
            CreatedAt = task.UpdatedAt,
            UpdatedAt = task.UpdatedAt,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);


        // =========================================================
        // 10. Save task + history
        // =========================================================

        await _context.SaveChangesAsync(
            cancellationToken);


        // =========================================================
        // 11. Send SignalR TaskAssigned event
        // =========================================================

        var notification =
            new TaskAssignedEvent(
                task.Id,
                staff.Id,
                staff.FullName,
                document.Id,
                document.DocumentNumber);

        await _realtime.SendTaskAssignedAsync(
            notification,
            cancellationToken);


        // =========================================================
        // 12. Return response
        // =========================================================

        return new AssignRenewalTaskResponseDto(
            task.Id,
            staff.Id,
            staff.FullName,
            task.Status,
            task.UpdatedAt);
    }
}