using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.DomainEvents;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Commands.UpdateRenewalTaskStatus;

public sealed class UpdateRenewalTaskStatusCommandHandler
    : IRequestHandler<
        UpdateRenewalTaskStatusCommand,
        UpdateRenewalTaskStatusResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;

    private static readonly Dictionary<RenewalTaskStatus, RenewalTaskStatus> AllowedTransitions = new()
    {
        { RenewalTaskStatus.Submitted,        RenewalTaskStatus.FeePaid },
        { RenewalTaskStatus.FeePaid,          RenewalTaskStatus.AwaitingApproval },
        { RenewalTaskStatus.AwaitingApproval, RenewalTaskStatus.Approved },
        { RenewalTaskStatus.Approved,         RenewalTaskStatus.Updated }
    };

    private static readonly RenewalTaskStatus[] StatusChain = new[]
    {
        RenewalTaskStatus.Submitted,
        RenewalTaskStatus.FeePaid,
        RenewalTaskStatus.AwaitingApproval,
        RenewalTaskStatus.Approved,
        RenewalTaskStatus.Updated
    };

    public UpdateRenewalTaskStatusCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IPublisher publisher)
    {
        _context = context;
        _currentUser = currentUser;
        _publisher = publisher;
    }

    public async Task<UpdateRenewalTaskStatusResponseDto> Handle(
        UpdateRenewalTaskStatusCommand request,
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
        // 2. Clients must not change task status
        // =========================================================

        if (_currentUser.Role == "Client")
        {
            throw new ForbiddenAccessException(
                "Clients are not allowed to change task status.");
        }


        // =========================================================
        // 3. Parse target status
        // =========================================================

        if (!Enum.TryParse<RenewalTaskStatus>(
                request.Status,
                ignoreCase: true,
                out var targetStatus))
        {
            throw new InvalidOperationException(
                $"Invalid status value: '{request.Status}'.");
        }


        // =========================================================
        // 4. Find renewal task
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
        // 4b. Agent ownership guard
        // =========================================================

        if (!_currentUser.IsAdmin && task.AssignedStaffId != currentUserId)
        {
            throw new KeyNotFoundException(
                "Renewal task was not found.");
        }


        // =========================================================
        // 5. Terminal status — cannot transition from Updated
        // =========================================================

        if (task.Status == RenewalTaskStatus.Updated)
        {
            throw new ConflictException(
                "This renewal task has already been completed.");
        }


        // =========================================================
        // 6. Validate transition
        // =========================================================

        if (targetStatus == RenewalTaskStatus.Blocked)
        {
            throw new InvalidOperationException(
                "Blocked status cannot be set through this endpoint.");
        }

        if (targetStatus == task.Status)
        {
            throw new InvalidOperationException(
                $"Task is already in {task.Status} status.");
        }

        if (_currentUser.IsAdmin)
        {
            var currentIndex =
                Array.IndexOf(StatusChain, task.Status);

            var targetIndex =
                Array.IndexOf(StatusChain, targetStatus);

            if (currentIndex < 0 ||
                targetIndex < 0 ||
                targetIndex <= currentIndex)
            {
                throw new InvalidOperationException(
                    $"Cannot transition from {task.Status} to {targetStatus}.");
            }
        }
        else
        {
            if (!AllowedTransitions.TryGetValue(
                    task.Status, out var next) ||
                next != targetStatus)
            {
                throw new InvalidOperationException(
                    $"Cannot transition from {task.Status} to {targetStatus}. " +
                    $"Only the next sequential status is allowed.");
            }
        }


        // =========================================================
        // 7. Validate ServiceFeeAmount when completing task
        // =========================================================

        if (targetStatus == RenewalTaskStatus.Updated)
        {
            if (!task.ServiceFeeAmount.HasValue ||
                task.ServiceFeeAmount.Value <= 0)
            {
                throw new ConflictException(
                    "Cannot complete renewal task: ServiceFeeAmount must be set and greater than 0.");
            }
        }


        // =========================================================
        // 8. Apply status update
        // =========================================================

        var previousStatus = task.Status;

        task.Status = targetStatus;
        task.UpdatedAt = DateTime.UtcNow;

        if (targetStatus == RenewalTaskStatus.Updated)
        {
            task.CompletedAt = DateTime.UtcNow;
        }


        // =========================================================
        // 9. Create task history
        // =========================================================

        var history = new RenewalTaskHistory
        {
            Id = Guid.NewGuid(),

            RenewalTaskId = task.Id,

            Status = targetStatus,

            ChangedBy = currentUserId,

            ChangedAt = task.UpdatedAt,

            Note =
                string.IsNullOrWhiteSpace(request.Note)
                    ? $"Status changed from {previousStatus} to {targetStatus}."
                    : request.Note,

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
            Action = "RenewalTaskStatusUpdated",
            EntityType = "RenewalTask",
            EntityId = task.Id,
            Description =
                $"Renewal task status changed from {previousStatus} to {targetStatus}.",
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
        // 11. Publish completion event (after save succeeds)
        // =========================================================

        if (previousStatus != RenewalTaskStatus.Updated &&
            targetStatus == RenewalTaskStatus.Updated)
        {
            await _publisher.Publish(
                new RenewalTaskCompletedEvent(
                    task.Id,
                    task.ClientCompanyId),
                cancellationToken);
        }


        // =========================================================
        // 12. Return response
        // =========================================================

        return new UpdateRenewalTaskStatusResponseDto(
            task.Id,
            previousStatus,
            task.Status,
            task.UpdatedAt);
    }
}
