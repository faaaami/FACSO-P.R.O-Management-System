using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Tasks.Commands.CancelRenewalTask;

public sealed class CancelRenewalTaskCommandHandler
    : IRequestHandler<
        CancelRenewalTaskCommand,
        CancelRenewalTaskResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CancelRenewalTaskCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CancelRenewalTaskResponseDto> Handle(
        CancelRenewalTaskCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validate authenticated user
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "Authenticated user was not found.");
        }

        // 2. Find task
        var task =
            await _context.RenewalTasks
                .FirstOrDefaultAsync(
                    x => x.Id == request.TaskId,
                    cancellationToken);

        if (task is null)
        {
            throw new KeyNotFoundException(
                "Renewal task was not found.");
        }

        // 3. Prevent cancelling an already deleted task
        if (task.IsDeleted)
        {
            throw new InvalidOperationException(
                "Renewal task is already cancelled.");
        }

        // 4. Soft delete
        var now = DateTime.UtcNow;

        task.IsDeleted = true;
        task.DeletedAt = now;
        task.UpdatedAt = now;

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "RenewalTaskCancelled",
            EntityType = "RenewalTask",
            EntityId = task.Id,
            Description =
                $"Renewal task was cancelled.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        // 5. Save
        await _context.SaveChangesAsync(
            cancellationToken);

        // 6. Return response
        return new CancelRenewalTaskResponseDto(
            task.Id,
            task.IsDeleted,
            task.DeletedAt!.Value);
    }
}