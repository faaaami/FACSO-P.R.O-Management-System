using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Tasks.Commands.AddRenewalStep;

public sealed class AddRenewalStepCommandHandler
    : IRequestHandler<
        AddRenewalStepCommand,
        AddRenewalStepResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AddRenewalStepCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<AddRenewalStepResponseDto> Handle(
        AddRenewalStepCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validate authenticated user
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "Authenticated user was not found.");
        }

        var currentUserId = _currentUser.UserId.Value;

        // 2. Validate step name
        if (string.IsNullOrWhiteSpace(request.StepName))
        {
            throw new ArgumentException(
                "Step name is required.");
        }

        var stepName = request.StepName.Trim();

        // 3. Find active task
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

        // 4. Create step log
        var now = DateTime.UtcNow;

        var stepLog = new RenewalStepLog
        {
            Id = Guid.NewGuid(),

            RenewalTaskId = task.Id,

            StepName = stepName,

            CompletedBy = currentUserId,

            CompletedAt = now,

            ReferenceNumber =
                string.IsNullOrWhiteSpace(
                    request.ReferenceNumber)
                    ? null
                    : request.ReferenceNumber.Trim(),

            ProofFileUrl =
                string.IsNullOrWhiteSpace(
                    request.ProofFileUrl)
                    ? null
                    : request.ProofFileUrl.Trim(),

            CreatedAt = now,

            UpdatedAt = now,

            IsDeleted = false,

            DeletedAt = null
        };

        _context.RenewalStepLogs.Add(stepLog);

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "RenewalStepAdded",
            EntityType = "RenewalStepLog",
            EntityId = stepLog.Id,
            Description =
                $"Renewal step '{stepLog.StepName}' was added to task {task.Id}.",
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
        return new AddRenewalStepResponseDto(
            stepLog.Id,
            stepLog.RenewalTaskId,
            stepLog.StepName,
            stepLog.CompletedBy,
            stepLog.CompletedAt,
            stepLog.ReferenceNumber,
            stepLog.ProofFileUrl);
    }
}