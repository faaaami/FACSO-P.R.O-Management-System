using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Commands.SetRenewalTaskServiceFee;

public sealed class SetRenewalTaskServiceFeeCommandHandler
    : IRequestHandler<
        SetRenewalTaskServiceFeeCommand,
        SetRenewalTaskServiceFeeResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SetRenewalTaskServiceFeeCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<SetRenewalTaskServiceFeeResponseDto> Handle(
        SetRenewalTaskServiceFeeCommand request,
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
        // 2. Admin only
        // =========================================================
        //
        // The route is already [Authorize(Roles = "Admin")], so this is a
        // second lock on the same door rather than the only one: the command
        // is a plain IRequest and can be sent through ISender from anywhere in
        // the application, including a future handler. The approved business
        // decision is that the Admin supplies the amount, so an Agent or Client
        // reaching this command is refused here with the same exception type
        // UpdateRenewalTaskStatusCommandHandler uses for a non-privileged role
        // change.

        if (!_currentUser.IsAdmin)
        {
            throw new ForbiddenAccessException(
                "Only an Admin can set the service fee for a renewal task.");
        }


        // =========================================================
        // 3. Find renewal task
        // =========================================================
        //
        // The query filters IsDeleted, so a soft-deleted task is indistinguishable
        // from a missing one — which is the same 404 shape the sibling task
        // commands return, and is the correct one to return rather than
        // confirming that a deleted task exists.

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
        // 4. The fee is frozen once the task is Updated
        // =========================================================
        //
        // `Updated` is terminal: UpdateRenewalTaskStatusCommandHandler already
        // refuses every transition out of it. A task in that state has been
        // completed and its Service Fee invoice was created by
        // CreateServiceFeeInvoiceOnTaskCompletedHandler at the moment it
        // completed, with Amount already copied from this field. Allowing an
        // edit now would leave the invoice and the task disagreeing, with no
        // way to reconcile them — the invoice is not editable through this
        // module and its own creation is one-per-task. The same guard is what
        // AssignRenewalTaskCommandHandler applies, for the same reason and with
        // the same message, so the two Admin mutations on a completed task
        // fail identically.

        if (task.Status == RenewalTaskStatus.Updated)
        {
            throw new InvalidOperationException(
                "Cannot change the service fee of a completed renewal task.");
        }


        // =========================================================
        // 5. Apply the new amount
        // =========================================================
        //
        // Written to the existing nullable RenewalTask.ServiceFeeAmount column.
        // No new field, no migration: the column already exists (see
        // AddServiceFeeInvoices) and is the one the completion gate and the
        // invoice-creation handler already read.
        //
        // The GreaterThan(0) rule lives in the validator, so it is enforced by
        // the normal FluentValidation pipeline as a 400. It is deliberately NOT
        // repeated as a second check here: UpdateRenewalTaskStatusCommandHandler
        // and CreateServiceFeeInvoiceOnTaskCompletedHandler both re-assert the
        // same rule because they are entry points of their own, whereas this
        // command has exactly one. Restating it would be a third copy that
        // could drift.

        var previousAmount = task.ServiceFeeAmount;

        task.ServiceFeeAmount = request.Amount;
        task.UpdatedAt = DateTime.UtcNow;

        var isChange = previousAmount.HasValue;

        // The change is described with both figures whenever there was a
        // previous one, so the audit trail records what the amount became and
        // not merely that something changed. A first-time set has no "from" to
        // report and says so rather than printing a placeholder zero, which
        // would read as a real prior value.
        var historyNote = isChange
            ? $"Service fee changed from {FormatAmount(previousAmount!.Value)} to {FormatAmount(request.Amount)}."
            : $"Service fee set to {FormatAmount(request.Amount)}.";


        // =========================================================
        // 6. Create task history
        // =========================================================
        //
        // Status carries the task's UNCHANGED status, exactly as
        // AssignRenewalTaskCommandHandler does. This is not a status transition
        // — it is an attribute change — and writing the same status the task
        // already holds is what keeps the History tab an event log rather than
        // an implied state timeline. The note is what distinguishes this row.
        // Note is a 1000-character column (RenewalTaskConfiguration); the text
        // is bounded by construction because both operands are formatted
        // decimals, not free text.

        var history = new RenewalTaskHistory
        {
            Id = Guid.NewGuid(),

            RenewalTaskId = task.Id,

            Status = task.Status,

            ChangedBy = currentUserId,

            ChangedAt = task.UpdatedAt,

            Note = historyNote,

            CreatedAt = task.UpdatedAt,

            UpdatedAt = task.UpdatedAt,

            IsDeleted = false,

            DeletedAt = null
        };

        _context.RenewalTaskHistories.Add(history);


        // =========================================================
        // 7. Create audit log
        // =========================================================
        //
        // Action follows the established inline-string convention — there is no
        // AuditAction enum or constant class in this codebase; every handler
        // writes its own literal ("RenewalTaskAssigned", "RenewalTaskBlocked",
        // "RenewalTaskStatusUpdated", "RenewalTaskCreated"). This one is
        // "RenewalTaskServiceFeeSet", a single name for both the first set and
        // a subsequent change, so a query for the action returns every fee
        // decision on the task.

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),

            UserId = currentUserId,

            Action = "RenewalTaskServiceFeeSet",

            EntityType = "RenewalTask",

            EntityId = task.Id,

            Description = isChange
                ? $"Renewal task service fee changed from {FormatAmount(previousAmount!.Value)} to {FormatAmount(request.Amount)}."
                : $"Renewal task service fee set to {FormatAmount(request.Amount)}.",

            CreatedAt = task.UpdatedAt,

            UpdatedAt = task.UpdatedAt,

            IsDeleted = false,

            DeletedAt = null
        };

        _context.AuditLogs.Add(auditLog);


        // =========================================================
        // 8. Save task + history + audit
        // =========================================================
        //
        // One SaveChanges, so the amount, the history row and the audit row
        // commit together. There is deliberately no Notification for the
        // assigned Agent and no SignalR event: the fee is an Admin-side input to
        // the eventual invoice and is not an event in the Agent's work, unlike
        // assignment, which reassigns their work. Adding either would be
        // inventing a notification the rest of the module does not imply.

        await _context.SaveChangesAsync(
            cancellationToken);


        // =========================================================
        // 9. Return response
        // =========================================================

        return new SetRenewalTaskServiceFeeResponseDto(
            task.Id,
            task.ServiceFeeAmount.Value,
            isChange,
            task.Status,
            task.UpdatedAt);
    }


    /// <summary>
    /// Formats a persisted amount for the history note and the audit
    /// description, using the same AED 2-decimal convention the
    /// CreateServiceFeeInvoiceOnTaskCompletedHandler billing path assumes.
    /// It is private to this handler on purpose: the two strings it produces
    /// are the only places a bare decimal is turned into text, and both are
    /// audit records rather than UI, so this is not a display formatter and
    /// does not belong in a shared presentation helper.
    /// </summary>
    private static string FormatAmount(decimal amount)
    {
        return $"AED {amount.ToString("F2")}";
    }
}
