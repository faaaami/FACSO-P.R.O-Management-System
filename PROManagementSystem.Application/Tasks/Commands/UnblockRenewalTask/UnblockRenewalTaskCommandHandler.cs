using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Commands.UnblockRenewalTask;

public sealed class UnblockRenewalTaskCommandHandler
    : IRequestHandler<
        UnblockRenewalTaskCommand,
        UnblockRenewalTaskResponseDto>
{
    /// <summary>
    /// Upper bound on the unblock reason.
    ///
    /// The reason is persisted twice, into two columns with two different limits:
    ///   - renewal_task_histories.note is varchar(1000), written as
    ///     "Unblocked: {reason}", so the reason must leave room for that prefix;
    ///   - audit_logs.description is varchar(500) — see AuditLogConfiguration —
    ///     and this command writes "Renewal task was unblocked: {reason}".
    ///
    /// 400 keeps both comfortably inside their columns, and it is stated as a
    /// server rule rather than a client hint: the two limits are a property of the
    /// schema, and a form that discovered them only on submit would be worse.
    ///
    /// Worth naming: this is the same bound, for the same column-budget reason,
    /// that BlockRenewalTaskCommandValidator applies to the block reason. Both
    /// commands compose audit_logs.description (varchar(500)) out of the reason,
    /// so an over-length value would overflow the description and fail the whole
    /// SaveChanges. The two bounds are deliberately kept identical; this command
    /// still enforces its own inline.
    /// </summary>
    private const int MaxUnblockReasonLength = 400;

    /// <summary>
    /// audit_logs.description is varchar(500), so the composed description is built
    /// against a hard budget instead of being assumed to fit.
    ///
    /// The UNBLOCK reason is never truncated here: it is already bounded by
    /// MaxUnblockReasonLength, so its slot always fits, and the history Note keeps
    /// it verbatim in any case.
    ///
    /// The CLEARED blocked reason is a different matter. It is not this request's
    /// input, so this command cannot bound it — it is whatever was stored on the
    /// task — and it shares a 500-char column with the unblock reason. It is
    /// therefore trimmed to whatever room is left and marked with an ellipsis, so a
    /// reader can tell a shortened value from a complete one rather than being
    /// quietly handed a partial reason as if it were the whole thing.
    ///
    /// The unabridged blocked reason is not at risk from this: it is already
    /// permanent in the block's own history row.
    /// </summary>
    private static string BuildAuditDescription(
        string unblockReason,
        string? previousBlockedReason)
    {
        const string prefix = "Renewal task was unblocked: ";
        const string clearedPrefix = " (cleared blocked reason: ";
        const int columnLimit = 500;

        if (string.IsNullOrWhiteSpace(previousBlockedReason))
        {
            return prefix + unblockReason;
        }

        // The unblock reason and the cleared reason SHARE this one 500-char
        // column, so the cleared reason is budgeted against what is left AFTER the
        // unblock reason, not against the column on its own. Budgeting it against
        // the full column is the obvious mistake and it overflows: at the 400-char
        // limit, "prefix + 400 + clearedPrefix + any reason + ')'" already exceeds
        // 500 on its own, which aborts the whole transaction with a 500.
        var available =
            columnLimit
            - prefix.Length
            - unblockReason.Length
            - clearedPrefix.Length
            - 1;

        // The budget can in principle go negative if MaxUnblockReasonLength were
        // ever raised past the column. Prefer dropping the optional clause over
        // throwing on a substring, so the write still succeeds and the audit row
        // simply carries the unblock reason.
        if (available < 2)
        {
            return prefix + unblockReason;
        }

        var cleared =
            previousBlockedReason!.Length > available
                ? previousBlockedReason[..(available - 1)] + "…"
                : previousBlockedReason;

        return $"{prefix}{unblockReason}{clearedPrefix}{cleared})";
    }

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UnblockRenewalTaskCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<UnblockRenewalTaskResponseDto> Handle(
        UnblockRenewalTaskCommand request,
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
        // BlockRenewalTaskCommandHandler can be reached by an Agent, so it uses an
        // OWNERSHIP guard — an Agent may block their own task. Unblocking is the
        // opposite decision: it is the Admin's alone, so this is a role refusal
        // rather than a 404, and the same shape
        // SetRenewalTaskServiceFeeCommandHandler uses for its Admin-only gate.
        //
        // The route is already [Authorize(Roles = "Admin")], so this is a second
        // lock on the same door rather than the only one. The command is a plain
        // IRequest and can be sent through ISender from anywhere in the
        // application, including a future handler.

        if (!_currentUser.IsAdmin)
        {
            throw new ForbiddenAccessException(
                "Only an Admin can unblock a renewal task.");
        }


        // =========================================================
        // 3. Validate reason
        // =========================================================
        //
        // Inlined, and deliberately so. BlockRenewalTaskCommandHandler now bounds
        // its reason through BlockRenewalTaskCommandValidator, which reports a
        // bad reason as a field-level VALIDATION_ERROR. This command keeps the
        // inline check and throws ArgumentException, which
        // GlobalExceptionMiddleware maps to 400 instead. Unifying the two would
        // change this endpoint's error shape, so the divergence is recorded here
        // rather than closed.
        //
        // Required, and whitespace is not a reason. IsNullOrWhiteSpace also
        // catches a null binding, which is what an omitted JSON property
        // produces.

        if (string.IsNullOrWhiteSpace(request.UnblockReason))
        {
            throw new ArgumentException(
                "Unblock reason is required.");
        }

        // Trimmed ONCE and measured from the trimmed value, because that is what
        // actually gets stored: a value of exactly the limit surrounded by spaces
        // must pass, and measuring the raw text would refuse it.
        var unblockReason =
            request.UnblockReason.Trim();

        if (unblockReason.Length > MaxUnblockReasonLength)
        {
            throw new ArgumentException(
                $"Unblock reason must not exceed {MaxUnblockReasonLength} characters.");
        }


        // =========================================================
        // 4. Find task
        // =========================================================
        //
        // Filters IsDeleted, so a soft-deleted task is indistinguishable from a
        // missing one — the same 404 shape the sibling task commands return, and
        // the correct one rather than confirming that a deleted task exists.

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
        // 5. The task must actually be Blocked
        // =========================================================
        //
        // This ONE check is the whole guard, and it is deliberately expressed as
        // "is it Blocked" rather than as a list of states that may be unblocked.
        // Anything that is not Blocked is refused, which covers every case the
        // workflow cares about without restating them:
        //
        //   - a Submitted / FeePaid / AwaitingApproval / Approved task, which was
        //     never blocked and has no BlockedReason to clear;
        //   - an Updated task, which is terminal and must stay that way;
        //   - a SECOND unblock of an already-unblocked task, because the first one
        //     already moved it out of Blocked.
        //
        // So "repeated unblock" needs no separate idempotency rule: after the
        // first success the task is Submitted, and this guard turns the second
        // attempt into the same 409 as any other non-Blocked task. An operation
        // that silently succeeded twice would be worse than one that is honest
        // about having nothing left to do.
        //
        // The message names the actual state so the caller can tell "wrong state"
        // from "not found" without a second request.

        if (task.Status != RenewalTaskStatus.Blocked)
        {
            throw new InvalidOperationException(
                $"Cannot unblock a renewal task with status {task.Status}. " +
                "Only a Blocked task can be unblocked.");
        }


        // =========================================================
        // 6. Return the task to Submitted and clear the blocked markers
        // =========================================================
        //
        // The approved business decision is Blocked -> Submitted, NOT a return to
        // whatever status the task held before it was blocked. Nothing in the
        // database records that prior status — BlockRenewalTaskCommandHandler
        // overwrites Status with Blocked and keeps no prior value — so restoring
        // it would mean inventing a field. Submitted is also the honest target:
        // it is the start of the chain, and the Agent re-drives the task forward
        // from there, which is the same work they would do for a new task.
        //
        // BlockedReason and BlockedSince are cleared, not preserved on the task.
        // They describe a CURRENT condition, and the condition has ended. The
        // reason is not lost: the block's own history row still holds it
        // permanently, and step 7 adds the unblock's reason beside it. That
        // history pair is the permanent record; these two columns are only ever
        // the current one.
        //
        // Nothing else on the task is touched. In particular AssignedStaffId is
        // left alone, so the Agent who was blocked is the Agent who resumes —
        // which is the point, since the work is the same work.

        var now = DateTime.UtcNow;

        var previousBlockedReason = task.BlockedReason;

        task.Status = RenewalTaskStatus.Submitted;
        task.BlockedReason = null;
        task.BlockedSince = null;
        task.UpdatedAt = now;


        // =========================================================
        // 7. Create history
        // =========================================================
        //
        // Status carries the task's NEW status (Submitted), exactly as
        // BlockRenewalTaskCommandHandler records the new status (Blocked). The
        // History tab is an event log, so consecutive rows legitimately repeat or
        // change a status; the Status column is what orders and distinguishes the
        // events.
        //
        // The Note is prefixed "Unblocked:" rather than being the bare reason, the
        // one deliberate departure from the block handler. Block writes its reason
        // verbatim, which is unambiguous there because only one kind of row ever
        // carries that Status. Here a Submitted row can equally be a status change
        // made through /status, so an unprefixed reason would leave the reader
        // unable to tell which kind of event the row records. The prefix costs
        // nothing and makes the event log self-describing.

        var history = new RenewalTaskHistory
        {
            Id = Guid.NewGuid(),

            RenewalTaskId = task.Id,

            Status = RenewalTaskStatus.Submitted,

            ChangedBy = currentUserId,

            ChangedAt = now,

            // The reason recorded is the UNBLOCK reason, not the original blocked
            // reason. The original is already permanent in the block's own history
            // row; repeating it here would suggest it had been lost. It is named in
            // the audit description instead, so a reader of the audit trail alone
            // can see what was cleared as well as what replaced it.
            Note = $"Unblocked: {unblockReason}",

            CreatedAt = now,

            UpdatedAt = now,

            IsDeleted = false,

            DeletedAt = null
        };

        _context.RenewalTaskHistories.Add(history);


        // =========================================================
        // 8. Create audit log
        // =========================================================
        //
        // "RenewalTaskUnblocked" is the counterpart to the existing
        // "RenewalTaskBlocked" literal. There is no AuditAction enum or constant
        // class in this codebase — every handler writes its own string — so this
        // follows that convention rather than introducing one.
        //
        // The description names the cleared blocked reason as well as the new one.
        // A 409 on a repeat attempt is therefore traceable: the audit log shows
        // both the unblock and, from an earlier row, the block it undid.

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),

            UserId = currentUserId,

            Action = "RenewalTaskUnblocked",

            EntityType = "RenewalTask",

            EntityId = task.Id,

            Description =
                BuildAuditDescription(
                    unblockReason,
                    previousBlockedReason),

            CreatedAt = now,

            UpdatedAt = now,

            IsDeleted = false,

            DeletedAt = null
        };

        _context.AuditLogs.Add(auditLog);


        // =========================================================
        // 9. Save task + history + audit
        // =========================================================
        //
        // One SaveChanges, so the status change, the cleared blocked markers, the
        // history row and the audit row commit together. This is the same
        // persistence pattern BlockRenewalTaskCommandHandler uses, and no explicit
        // transaction is introduced: a single SaveChanges is already one unit of
        // work.
        //
        // There is deliberately no Notification for the assigned Agent and no
        // SignalR event. The block operation that this mirrors sends neither, so
        // an unblock that sent one would make the pair asymmetric — the Agent
        // would learn about a task resuming from a push while the blocking that
        // paused it arrived silently. Neither is in scope for this workflow.

        await _context.SaveChangesAsync(
            cancellationToken);


        // =========================================================
        // 10. Return response
        // =========================================================

        return new UnblockRenewalTaskResponseDto(
            task.Id,
            task.Status,
            unblockReason,
            task.UpdatedAt);
    }
}
