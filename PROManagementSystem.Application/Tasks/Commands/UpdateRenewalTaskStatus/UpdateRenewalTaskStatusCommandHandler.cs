using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PROManagementSystem.Application.Common.DomainEvents;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Realtime;
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
    private readonly IRealtimeNotificationService _realtime;
    private readonly IPendingNotificationPushQueue _pendingPushes;
    private readonly ILogger<UpdateRenewalTaskStatusCommandHandler> _logger;

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
        IPublisher publisher,
        IRealtimeNotificationService realtime,
        IPendingNotificationPushQueue pendingPushes,
        ILogger<UpdateRenewalTaskStatusCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _publisher = publisher;
        _realtime = realtime;
        _pendingPushes = pendingPushes;
        _logger = logger;
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
        // 4. Completion unit of work
        // =========================================================
        //
        // The status change, the document expiry, the history row, both audit
        // rows, the agent notification, the client notification and the service
        // fee invoice raised by the completion event handler are one unit of
        // work and must commit together. Previously the task side was saved and
        // committed first and the invoice was written by a later SaveChanges,
        // so a failure in between left a completed task with no invoice and no
        // way to replay the completion.
        //
        // The task row is locked FOR UPDATE and re-read inside the transaction.
        // The lock has to be taken before the read that decides whether
        // completion is allowed: without it two completion requests both read
        // Approved, both pass the terminal check below, and both commit, which
        // duplicates the history row, the status audit and the invoice. Locking
        // first makes the loser wait and then observe the committed Updated
        // state, so it exits on the same terminal conflict a repeat request
        // already produced.

        await using var transaction =
            await _context.BeginTransactionAsync(
                cancellationToken);

        // Captured so the response and the realtime push after the commit do
        // not depend on the tracked entity still being in scope.
        Guid assignedStaffId;
        DateTime updatedAt;
        RenewalTaskStatus previousStatus;

        try
        {
            // =====================================================
            // 4a. Lock and read the renewal task
            // =====================================================

            var lockedTasks =
                await _context.RenewalTasks
                    .FromSqlInterpolated(
                        $"SELECT * FROM renewal_tasks WHERE \"Id\" = {request.TaskId} AND \"IsDeleted\" = false FOR UPDATE")
                    .ToListAsync(cancellationToken);

            var task = lockedTasks.SingleOrDefault();

            if (task is null)
            {
                throw new KeyNotFoundException(
                    "Renewal task was not found.");
            }


            // =====================================================
            // 4b. Agent ownership guard
            // =====================================================

            if (!_currentUser.IsAdmin && task.AssignedStaffId != currentUserId)
            {
                throw new KeyNotFoundException(
                    "Renewal task was not found.");
            }


            // =====================================================
            // 5. Terminal status — cannot transition from Updated
            // =====================================================

            if (task.Status == RenewalTaskStatus.Updated)
            {
                throw new ConflictException(
                    "This renewal task has already been completed.");
            }


            // =====================================================
            // 6. Validate transition
            // =====================================================

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


            // =====================================================
            // 7. Validate ServiceFeeAmount when completing task
            // =====================================================

            if (targetStatus == RenewalTaskStatus.Updated)
            {
                if (!task.ServiceFeeAmount.HasValue ||
                    task.ServiceFeeAmount.Value <= 0)
                {
                    throw new ConflictException(
                        "Cannot complete renewal task: ServiceFeeAmount must be set and greater than 0.");
                }
            }


            // =====================================================
            // 7b. Resolve the linked document when completing
            // =====================================================
            //
            // The document is reached only through the task's own DocumentId — never
            // from the request — and only when the transition completes the renewal,
            // so any other status change does not require the document to exist.

            Document? linkedDocument = null;
            DateTime? previousExpiryDate = null;

            if (targetStatus == RenewalTaskStatus.Updated)
            {
                linkedDocument =
                    await _context.Documents
                        .FirstOrDefaultAsync(
                            x =>
                                x.Id == task.DocumentId &&
                                !x.IsDeleted,
                            cancellationToken);

                if (linkedDocument is null)
                {
                    throw new KeyNotFoundException(
                        "The document linked to this task was not found.");
                }

                if (!request.RenewedExpiryDate.HasValue)
                {
                    throw new InvalidOperationException(
                        "A renewed expiry date is required when the task status is set to Updated.");
                }

                // Same DATE rule as the validator, and enforced again here
                // because this is the write path. The value is persisted
                // through ToUtcExpiryDate, which keeps only the date component,
                // so the comparison must be made on the same basis: the
                // normalized date strictly after today's UTC date. Comparing the
                // raw timestamp admitted a value that then persisted as an
                // already-expired midnight.
                if (request.RenewedExpiryDate.Value.Date <= DateTime.UtcNow.Date)
                {
                    throw new InvalidOperationException(
                        "The renewed expiry date must be a date after today (UTC).");
                }

                previousExpiryDate = linkedDocument.ExpiryDate;
            }


            // =====================================================
            // 8. Apply status update
            // =====================================================

            previousStatus = task.Status;

            task.Status = targetStatus;
            task.UpdatedAt = DateTime.UtcNow;

            if (targetStatus == RenewalTaskStatus.Updated)
            {
                task.CompletedAt = DateTime.UtcNow;
            }


            // =====================================================
            // 8b. Apply the renewed expiry to the linked document
            // =====================================================
            //
            // The value stored is the date the reader entered, not the moment the
            // renewal was processed. ToUtcExpiryDate explains the anchoring it
            // needs before Npgsql will accept it.

            if (linkedDocument is not null)
            {
                linkedDocument.ExpiryDate = ToUtcExpiryDate(
                    request.RenewedExpiryDate!.Value);
                linkedDocument.UpdatedAt = task.UpdatedAt;
            }


            // =====================================================
            // 9. Create task history
            // =====================================================

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

            // =====================================================
            // 9a. Audit the document expiry change
            // =====================================================
            //
            // A separate row on the Document, not a second field on the task audit
            // above, so the audit trail records what happened to the document
            // itself alongside the task that triggered it.

            if (linkedDocument is not null)
            {
                _context.AuditLogs.Add(new Domain.Entities.AuditLog
                {
                    Id = Guid.NewGuid(),

                    UserId = _currentUser.UserId,

                    Action = "DocumentRenewed",

                    EntityType = "Document",

                    EntityId = linkedDocument.Id,

                    Description =
                        BuildDocumentRenewedDescription(
                            linkedDocument.DocumentNumber,
                            previousExpiryDate,
                            request.RenewedExpiryDate!.Value),

                    Metadata =
                        BuildDocumentRenewedMetadata(
                            previousExpiryDate,
                            request.RenewedExpiryDate!.Value),

                    CreatedAt = task.UpdatedAt,

                    UpdatedAt = task.UpdatedAt,

                    IsDeleted = false,

                    DeletedAt = null
                });
            }

            // =====================================================
            // 9b. Persist a notification for the assigned agent
            // =====================================================

            if (task.AssignedStaffId.HasValue)
            {
                var statusNotification = new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = task.AssignedStaffId.Value,
                    Title = "Task status changed",
                    Message =
                        $"TaskStatusChanged | TaskId:{task.Id} | " +
                        $"Status changed from {previousStatus} to {targetStatus}.",
                    IsRead = false,
                    ReadAt = null,
                    CreatedAt = task.UpdatedAt,
                    UpdatedAt = task.UpdatedAt,
                    IsDeleted = false,
                    DeletedAt = null
                };

                _context.Notifications.Add(statusNotification);
            }


            // =====================================================
            // 10. Save task + history
            // =====================================================
            //
            // Part of the transaction opened in step 4, so it is not committed
            // on its own.

            await _context.SaveChangesAsync(
                cancellationToken);


            // =====================================================
            // 11. Publish completion event
            // =====================================================
            //
            // Still inside the transaction: the completion handlers write the
            // client notifications and the service fee invoice through the same
            // scoped context, so their SaveChanges enlist in this transaction
            // and commit together with the status change above. If invoice
            // creation throws, everything rolls back and the task is left in
            // its previous state, still retryable.

            if (previousStatus != RenewalTaskStatus.Updated &&
                targetStatus == RenewalTaskStatus.Updated)
            {
                await _publisher.Publish(
                    new RenewalTaskCompletedEvent(
                        task.Id,
                        task.ClientCompanyId),
                    cancellationToken);
            }

            updatedAt = task.UpdatedAt;

            assignedStaffId = task.AssignedStaffId ?? Guid.Empty;

            // =====================================================
            // 11a. Commit the completion unit of work
            // =====================================================

            await transaction.CommitAsync(
                cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(
                cancellationToken);
            throw;
        }


        // =========================================================
        // 11b. Send SignalR TaskStatusChanged event to the assigned agent
        // =========================================================
        //
        // After the commit on purpose. This is external work over the network,
        // so it must not run while the transaction and the task row lock are
        // still open.

        if (assignedStaffId != Guid.Empty)
        {
            var statusChanged =
                new TaskStatusChangedEvent(
                    request.TaskId,
                    assignedStaffId,
                    previousStatus,
                    targetStatus,
                    updatedAt);

            // P10.7: this send is best-effort. The completion unit of work is
            // already durable at this point, so a realtime transport failure
            // must be logged rather than reported as a failed completion. The
            // try/catch ends here, before the flush below, so that a TaskStatus
            // push failure can never skip the client completion notifications.
            try
            {
                await _realtime.SendTaskStatusChangedAsync(
                    statusChanged,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to push TaskStatusChanged for task {TaskId}.",
                    request.TaskId);
            }
        }


        // =========================================================
        // 11c. Flush the client completion notifications enqueued by the
        //      completion event handler
        // =========================================================
        //
        // After the commit on purpose, and only on the success path: the
        // completion handler queued these pushes while the transaction was still
        // open, so a client is never told about a completion that rolled back.
        // A delivery failure here is logged and swallowed inside the queue — the
        // operation is already durable and must not be reported as failed.

        await _pendingPushes.FlushAsync(
            cancellationToken);


        // =========================================================
        // 12. Return response
        // =========================================================

        return new UpdateRenewalTaskStatusResponseDto(
            request.TaskId,
            previousStatus,
            targetStatus,
            updatedAt);
    }

    private static string BuildDocumentRenewedDescription(
        string documentNumber,
        DateTime? previousExpiryDate,
        DateTime newExpiryDate)
    {
        const int columnLimit = 500;

        var previous =
            FormatExpiryDate(previousExpiryDate);

        var description =
            $"Document {documentNumber} renewed. Expiry date updated from {previous} to {FormatExpiryDate(newExpiryDate)}.";

        return description.Length > columnLimit
            ? description[..(columnLimit - 1)] + "…"
            : description;
    }

    private static string BuildDocumentRenewedMetadata(
        DateTime? previousExpiryDate,
        DateTime newExpiryDate)
    {
        return
            $"oldExpiryDate={FormatExpiryDate(previousExpiryDate)};" +
            $"newExpiryDate={FormatExpiryDate(newExpiryDate)}";
    }

    private static string FormatExpiryDate(DateTime? expiryDate)
    {
        return expiryDate.HasValue
            ? expiryDate.Value.ToString("yyyy-MM-dd")
            : "none";
    }

    /// <summary>
    /// Anchors the supplied date to UTC midnight.
    ///
    /// A date-only JSON value such as "2027-03-14" binds with
    /// DateTimeKind.Unspecified, and Npgsql refuses to write an unspecified
    /// DateTime to a timestamptz column, so storing the bound value verbatim
    /// fails the whole transaction. Taking the date component and marking it
    /// UTC keeps the value the date the reader chose instead of the moment the
    /// request was processed, makes the stored value independent of whatever
    /// offset the client sent, and adds no timezone conversion of its own.
    /// </summary>
    private static DateTime ToUtcExpiryDate(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    }
}
