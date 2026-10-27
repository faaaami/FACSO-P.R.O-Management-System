using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Notifications;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Commands.CancelRenewalTask;

public sealed class CancelRenewalTaskCommandHandler
    : IRequestHandler<
        CancelRenewalTaskCommand,
        CancelRenewalTaskResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserNotifier _notifier;

    public CancelRenewalTaskCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IUserNotifier notifier)
    {
        _context = context;
        _currentUser = currentUser;
        _notifier = notifier;
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

        // 3b. Updated is terminal and must not be soft-deleted
        //
        // `Updated` is the terminal state of the status chain and the value the
        // IX_renewal_tasks_DocumentId unique index excludes (Status != 6), so a
        // completed renewal is a finished piece of work rather than an open one.
        // Soft-deleting it would hide the completion record, the renewed document
        // date and the Service Fee invoice created at completion, behind a
        // cancellation that reads as "this never happened". The block and status
        // handlers already refuse to move a task out of Updated for the same
        // reason; cancelling was the one mutation that did not.
        if (task.Status == RenewalTaskStatus.Updated)
        {
            throw new ConflictException(
                "Cannot cancel a completed renewal task.");
        }

        // 3c. Document number for the audit description
        //
        // IgnoreQueryFilters so the number is still available when the document
        // itself has been soft-deleted: cancelling is an administrative action
        // over historical rows, and an audit trail that loses the document it
        // refers to is exactly the case where it is most needed. A missing row
        // degrades the description rather than failing the cancellation, which
        // the caller needs to succeed.
        var documentNumber =
            await _context.Documents
                .IgnoreQueryFilters()
                .Where(x => x.Id == task.DocumentId)
                .Select(x => x.DocumentNumber)
                .FirstOrDefaultAsync(
                    cancellationToken);

        // Named before the soft delete, while the task is still readable.
        var label = await NotificationLabels.RenewalTaskAsync(
            _context, task.Id, cancellationToken);

        // 4. Soft delete
        var now = DateTime.UtcNow;

        task.IsDeleted = true;
        task.DeletedAt = now;
        task.UpdatedAt = now;

        // 5. Create history
        //
        // Every sibling task mutation writes one of these, and the history tab
        // and GET /admin/tasks/{id}/history are built on it. A cancellation
        // without a history row was the gap: the task disappeared from every
        // read path with no entry explaining why, so the timeline simply ended.
        //
        // There is no Cancelled member in RenewalTaskStatus — cancellation is a
        // soft delete, not a state — so the row records the status the task held
        // at the moment it was cancelled, and the note says what happened. The
        // note is what distinguishes this row from an ordinary status change to
        // the same value.
        var history = new RenewalTaskHistory
        {
            Id = Guid.NewGuid(),

            RenewalTaskId = task.Id,

            Status = task.Status,

            ChangedBy = _currentUser.UserId.Value,

            ChangedAt = now,

            Note = "Cancelled",

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
            Action = "RenewalTaskCancelled",
            EntityType = "RenewalTask",
            EntityId = task.Id,
            //
            // Names the document, as the sibling lifecycle handlers do. "Renewal
            // task was cancelled." identified nothing beyond the entity id, and
            // the entity id is not what a reader of the audit trail recognises.
            // document_number is varchar(100), so this stays far inside
            // description's 500 even with a maximal document number.
            Description =
                documentNumber is null
                    ? "Renewal task was cancelled."
                    : $"Renewal task was cancelled for document " +
                      $"'{documentNumber}'.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        // 6. The assigned agent stops working on it. The notifier saves the
        //    cancellation and the notification in one write.
        if (task.AssignedStaffId.HasValue &&
            task.AssignedStaffId.Value != _currentUser.UserId.Value)
        {
            await _notifier.NotifyUsersAsync(
                [task.AssignedStaffId.Value],
                "Task cancelled",
                $"{label} was cancelled. No further work is needed on it.",
                cancellationToken);
        }

        // 7. Save
        await _context.SaveChangesAsync(
            cancellationToken);

        // 8. Return response
        return new CancelRenewalTaskResponseDto(
            task.Id,
            task.IsDeleted,
            task.DeletedAt!.Value);
    }
}