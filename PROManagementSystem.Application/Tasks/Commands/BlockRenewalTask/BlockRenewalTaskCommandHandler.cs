using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Commands.BlockRenewalTask;

public sealed class BlockRenewalTaskCommandHandler
    : IRequestHandler<
        BlockRenewalTaskCommand,
        BlockRenewalTaskResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public BlockRenewalTaskCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<BlockRenewalTaskResponseDto> Handle(
        BlockRenewalTaskCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validate authenticated user
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "Authenticated user was not found.");
        }

        var currentUserId = _currentUser.UserId.Value;

        // 2. Validate reason
        if (string.IsNullOrWhiteSpace(request.BlockedReason))
        {
            throw new ArgumentException(
                "Blocked reason is required.");
        }

        var blockedReason =
            request.BlockedReason.Trim();

        // 3. Find task
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

        // 3b. Agent ownership guard
        if (!_currentUser.IsAdmin && task.AssignedStaffId != currentUserId)
        {
            throw new KeyNotFoundException(
                "Renewal task was not found.");
        }

        // 4. Updated is terminal
        if (task.Status == RenewalTaskStatus.Updated)
        {
            throw new InvalidOperationException(
                "Cannot block a completed renewal task.");
        }

        // 5. Update task
        var now = DateTime.UtcNow;

        task.Status = RenewalTaskStatus.Blocked;
        task.BlockedReason = blockedReason;
        task.BlockedSince = now;
        task.UpdatedAt = now;

        // 6. Create history
        var history = new RenewalTaskHistory
        {
            Id = Guid.NewGuid(),

            RenewalTaskId = task.Id,

            Status = RenewalTaskStatus.Blocked,

            ChangedBy = currentUserId,

            ChangedAt = now,

            Note = blockedReason,

            CreatedAt = now,

            UpdatedAt = now,

            IsDeleted = false,

            DeletedAt = null
        };

        _context.RenewalTaskHistories.Add(history);

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "RenewalTaskBlocked",
            EntityType = "RenewalTask",
            EntityId = task.Id,
            Description =
                $"Renewal task was blocked: {blockedReason}",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        // 7. Save task + history
        await _context.SaveChangesAsync(
            cancellationToken);

        // 8. Return response
        return new BlockRenewalTaskResponseDto(
            task.Id,
            task.Status,
            task.BlockedReason!,
            task.BlockedSince!.Value,
            task.UpdatedAt);
    }
}