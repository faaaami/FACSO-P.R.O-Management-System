using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.DomainEvents;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Tasks.Commands.CreateRenewalTask;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceRequests.Commands.ConvertServiceRequest;

public sealed class ConvertServiceRequestCommandHandler
    : IRequestHandler<
        ConvertServiceRequestCommand,
        ConvertServiceRequestResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IDuplicateDocumentTranslator _duplicateTranslator;

    public ConvertServiceRequestCommandHandler(
        IApplicationDbContext context,
        ISender sender,
        ICurrentUserService currentUser,
        IPublisher publisher,
        IDuplicateDocumentTranslator duplicateTranslator)
    {
        _context = context;
        _sender = sender;
        _currentUser = currentUser;
        _publisher = publisher;
        _duplicateTranslator = duplicateTranslator;
    }

    public async Task<ConvertServiceRequestResponseDto> Handle(
        ConvertServiceRequestCommand request,
        CancellationToken cancellationToken)
    {
        // The renewal task and the request's own "Converted" transition are
        // one business decision, so they are written in a single transaction.
        // Without it, a failure after CreateRenewalTaskCommandHandler has
        // saved would leave a live task behind while the request still reads
        // Submitted, and the retry would then fail on the active-task guard.
        await using var transaction =
            await _context.BeginTransactionAsync(cancellationToken);

        // Guards the catch below. Once the commit lands the transaction is spent,
        // so a failure raised by the post-commit publish must not be answered
        // with a rollback attempt against a completed transaction: that throws
        // "This transaction has completed", which would both mask the real cause
        // and report a failure for a conversion that actually succeeded.
        var committed = false;

        try
        {
            // 1. Fast existence check. Deliberately untracked: the locked read
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

            // 2. Lock the request row. Reject takes the same lock, so a
            //    conversion and a rejection are serialised instead of racing
            //    into a Converted request that also carries a reason.
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

            // 3. MANDATORY post-lock re-check. A competing rejection may have
            //    landed while we waited for the lock.
            if (serviceRequest.Status !=
                ServiceRequestStatus.Submitted)
            {
                throw new ConflictException(
                    "Only submitted service requests can be converted.");
            }

            if (!serviceRequest.DocumentId.HasValue)
            {
                throw new ConflictException(
                    "No document linked to this request.");
            }

            // 4. The document must belong to the same company as the request.
            //    Task creation resolves its own company from the document, so
            //    without this an inconsistent link would silently create the
            //    task under the wrong company.
            await EnsureDocumentBelongsToRequestCompanyAsync(
                serviceRequest,
                cancellationToken);

            var now = DateTime.UtcNow;

            // 5. Reuse the existing Day 4 renewal-task creation flow. Its
            //    SaveChanges enlists in this transaction, so the task and the
            //    transition below commit or roll back together.
            var renewalTask =
                await _sender.Send(
                    new CreateRenewalTaskCommand(
                        serviceRequest.DocumentId.Value),
                    cancellationToken);

            serviceRequest.Status =
                ServiceRequestStatus.Converted;

            serviceRequest.ConvertedAt = now;

            serviceRequest.ConvertedRenewalTaskId = renewalTask.Id;

            serviceRequest.UpdatedAt = now;

            var auditLog = new Domain.Entities.AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = _currentUser.UserId,
                Action = "ServiceRequestConverted",
                EntityType = "ServiceRequest",
                EntityId = serviceRequest.Id,
                Description =
                    $"Service request was converted to renewal task {renewalTask.Id}.",
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            };
            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            committed = true;

            // 6. Published after the commit so a notification failure can
            //    never roll back a conversion that has already succeeded. A
            //    failure here is still surfaced, because silently swallowing it
            //    would hide a notification that never reached the client.
            await _publisher.Publish(
                new ServiceRequestDecidedEvent(
                    serviceRequest.Id,
                    serviceRequest.ClientCompanyId,
                    ServiceRequestStatus.Converted,
                    renewalTask.Id,
                    null),
                cancellationToken);

            return new ConvertServiceRequestResponseDto(
                serviceRequest.Id,
                renewalTask.Id,
                serviceRequest.Status,
                serviceRequest.ConvertedAt.Value);
        }
        catch (DbUpdateException exception)
        {
            // Ordering matters. A duplicate renewal task loses the race on
            // IX_renewal_tasks_DocumentId inside the inner CreateRenewalTask
            // send, which surfaces here as a DbUpdateException; the same
            // translation as the direct create path turns it into a 409 CONFLICT
            // instead of a 500. The rollback is repeated here rather than left to
            // the bare catch below because an exception rethrown from a catch
            // block is not caught by a sibling catch of the same try, so the
            // transaction would otherwise stay open.
            //
            // The request is left Submitted: the whole conversion is one
            // transaction, so a rolled-back convert is a convert that did not
            // happen, and the client can retry against the now-existing task.
            // Only this one constraint is translated; any other
            // DbUpdateException is rethrown as itself so a real database fault
            // is not reported as a domain conflict.
            if (!committed)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw _duplicateTranslator.Translate(exception) ??
                  (Exception)exception;
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
    /// Refuses to convert when the linked document resolves to a different
    /// client company than the request was raised under.
    /// </summary>
    private async Task EnsureDocumentBelongsToRequestCompanyAsync(
        Domain.Entities.ServiceRequest serviceRequest,
        CancellationToken cancellationToken)
    {
        var document =
            await _context.Documents
                .AsNoTracking()
                .Include(x => x.Employee)
                    .ThenInclude(x => x!.ClientEntity)
                .Include(x => x.ClientEntity)
                .FirstOrDefaultAsync(
                    x => x.Id == serviceRequest.DocumentId!.Value,
                    cancellationToken);

        if (document is null)
        {
            throw new ConflictException(
                "The document linked to this request no longer exists.");
        }

        Guid? documentClientCompanyId = null;

        if (document.ClientEntityId.HasValue)
        {
            documentClientCompanyId =
                document.ClientEntity?.ClientCompanyId;
        }
        else if (document.EmployeeId.HasValue)
        {
            documentClientCompanyId =
                document.Employee?
                    .ClientEntity?
                    .ClientCompanyId;
        }

        if (!documentClientCompanyId.HasValue)
        {
            // The rule is equality between the document's company and the
            // request's company, and "we could not work out the document's
            // company" is not that equality. Letting it through would leave
            // task creation to resolve the company on its own terms, which is
            // precisely the silent-wrong-company outcome this check exists to
            // prevent.
            throw new ConflictException(
                "The document linked to this request could not be resolved to a client company.");
        }

        if (documentClientCompanyId.Value !=
            serviceRequest.ClientCompanyId)
        {
            throw new ConflictException(
                "The document linked to this request belongs to a different client company.");
        }
    }
}
