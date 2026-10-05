using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.DomainEvents;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceRequests.Commands.RejectServiceRequest;

public sealed class RejectServiceRequestCommandHandler
    : IRequestHandler<
        RejectServiceRequestCommand,
        RejectServiceRequestResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;

    public RejectServiceRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IPublisher publisher)
    {
        _context = context;
        _currentUser = currentUser;
        _publisher = publisher;
    }

    public async Task<RejectServiceRequestResponseDto> Handle(
        RejectServiceRequestCommand request,
        CancellationToken cancellationToken)
    {
        // An explicit transaction is required, not just defensive: the
        // FOR UPDATE lock below is only held until the transaction ends, so
        // in autocommit mode it would be released before the write and would
        // not serialise anything against a concurrent conversion.
        await using var transaction =
            await _context.BeginTransactionAsync(cancellationToken);

        // Guards the catch below. Once the commit lands the transaction is spent,
        // so a failure raised by the post-commit publish must not be answered
        // with a rollback attempt against a completed transaction: that throws
        // "This transaction has completed", which would both mask the real cause
        // and report a failure for a rejection that actually succeeded.
        var committed = false;

        try
        {
            // 1. Fast existence check. Untracked on purpose: the locked read
            //    below is the read whose state we act on.
            var exists = await _context.ServiceRequests
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == request.ServiceRequestId &&
                         !x.IsDeleted,
                    cancellationToken);

            if (!exists)
            {
                throw new NotFoundException(
                    "Service request was not found.");
            }

            // 2. Lock the request row. ConvertServiceRequest takes the same
            //    lock, so the two decisions cannot both be applied and leave
            //    a Converted request carrying a rejection reason.
            var lockedRequests =
                await _context.ServiceRequests
                    .FromSqlInterpolated(
                        $"SELECT * FROM service_requests WHERE \"Id\" = {request.ServiceRequestId} AND \"IsDeleted\" = false FOR UPDATE")
                    .ToListAsync(cancellationToken);

            var serviceRequest = lockedRequests.SingleOrDefault();

            if (serviceRequest is null)
            {
                throw new NotFoundException(
                    "Service request was not found.");
            }

            // 3. MANDATORY post-lock re-check.
            if (serviceRequest.Status !=
                ServiceRequestStatus.Submitted)
            {
                throw new ConflictException(
                    "Only submitted service requests can be rejected.");
            }

            var reason = request.Reason.Trim();
            var now = DateTime.UtcNow;

            serviceRequest.Status =
                ServiceRequestStatus.Rejected;

            serviceRequest.RejectionReason = reason;

            serviceRequest.RejectedAt = now;

            serviceRequest.UpdatedAt = now;

            var auditLog = new Domain.Entities.AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = _currentUser.UserId,
                Action = "ServiceRequestRejected",
                EntityType = "ServiceRequest",
                EntityId = serviceRequest.Id,
                Description =
                    BuildAuditDescription(reason),
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            };
            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            committed = true;

            // Published after the commit so a notification failure can never
            // roll back a rejection that has already been recorded. A failure
            // here is still surfaced, because silently swallowing it would hide
            // a notification that never reached the client.
            await _publisher.Publish(
                new ServiceRequestDecidedEvent(
                    serviceRequest.Id,
                    serviceRequest.ClientCompanyId,
                    ServiceRequestStatus.Rejected,
                    null,
                    reason),
                cancellationToken);

            return new RejectServiceRequestResponseDto(
                serviceRequest.Id,
                serviceRequest.Status,
                serviceRequest.RejectionReason,
                serviceRequest.RejectedAt.Value);
        }
        catch
        {
            if (!committed)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
    }

    /// <summary>
    /// Builds the rejection audit description within the column's width.
    ///
    /// AuditLog.Description is nvarchar(500), while a rejection reason is valid up
    /// to 1000 characters. Interpolating the whole reason therefore overflowed the
    /// column and turned a perfectly valid rejection into a 500 — the decision was
    /// rolled back and the admin was told the request could not be rejected at all.
    ///
    /// The reason is only quoted in the audit entry; the authoritative copy is
    /// ServiceRequest.RejectionReason, which the client reads, and which holds the
    /// full text. So a bounded preview is enough for the audit trail to answer "why
    /// was this rejected" without duplicating the field or overflowing.
    /// </summary>
    private static string BuildAuditDescription(string reason)
    {
        const string prefix = "Service request was rejected: ";
        const string ellipsis = "…";

        // The ellipsis is charged to the same budget as the reason, so the
        // description can never reach prefix + budget + 1 and overflow.
        var budget = AuditDescriptionMaxLength - prefix.Length - ellipsis.Length;

        var preview = reason.Length > budget
            ? reason[..Math.Max(budget, 0)] + ellipsis
            : reason;

        return prefix + preview;
    }

    /// <summary>
    /// AuditLog.Description's configured max length. Kept as a constant so the
    /// truncation above has one source of truth to reason about.
    /// </summary>
    private const int AuditDescriptionMaxLength = 500;
}
