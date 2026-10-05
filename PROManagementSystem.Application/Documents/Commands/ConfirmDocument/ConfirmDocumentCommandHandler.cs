using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Documents.Commands.ConfirmDocument;

public sealed class ConfirmDocumentCommandHandler
    : IRequestHandler<
        ConfirmDocumentCommand,
        ConfirmDocumentResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ConfirmDocumentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ConfirmDocumentResponseDto> Handle(
        ConfirmDocumentCommand request,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _context.BeginTransactionAsync(cancellationToken);

        var committed = false;

        try
        {
            // 1. Lock the extraction row. The SELECT ... FOR UPDATE is the
            //    serialisation point for concurrent confirmations of the same
            //    extraction: the second request blocks until the first commits
            //    and then observes Status = "Confirmed". Raw SQL does not apply
            //    global query filters, so the soft-delete predicate is explicit.
            var locked =
                await _context.DocumentExtractions
                    .FromSqlInterpolated(
                        $"SELECT * FROM document_extractions WHERE \"Id\" = {request.ExtractionId} AND \"IsDeleted\" = false FOR UPDATE")
                    .ToListAsync(cancellationToken);

            var extraction = locked.SingleOrDefault();

            if (extraction is null)
            {
                throw new KeyNotFoundException(
                    "Document extraction was not found.");
            }

            // 2. Do not allow an already processed extraction
            if (extraction.IsDeleted)
            {
                throw new InvalidOperationException(
                    "Document extraction has been deleted.");
            }

            if (extraction.Status != "Pending")
            {
                throw new InvalidOperationException(
                    $"Document extraction has already been processed. " +
                    $"Current status: {extraction.Status}.");
            }

            // Expiry enforcement: a Pending extraction past its ExpiresAt is
            // marked Expired and rejected. NULL ExpiresAt means never-expiring
            // (legacy rows).
            var now = DateTime.UtcNow;

            if (extraction.ExpiresAt.HasValue &&
                extraction.ExpiresAt.Value <= now)
            {
                extraction.Status = "Expired";
                extraction.UpdatedAt = now;

                var expiryAudit = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = _currentUser.UserId,
                    Action = "DocumentExtractionExpired",
                    EntityType = "DocumentExtraction",
                    EntityId = extraction.Id,
                    Description =
                        "Document extraction has expired.",
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                };
                _context.AuditLogs.Add(expiryAudit);

                // The Expired transition + audit are intentionally committed
                // BEFORE the 409 is returned, preserving the existing
                // observable behavior (ExtractionExpiryEnforcementTests).
                await _context.SaveChangesAsync(
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                committed = true;

                throw new InvalidOperationException(
                    "Document extraction has expired and cannot be confirmed.");
            }

            // 3. Exactly one owner must be supplied
            var hasEmployee =
                request.EmployeeId.HasValue;

            var hasClientEntity =
                request.ClientEntityId.HasValue;

            if (hasEmployee == hasClientEntity)
            {
                throw new InvalidOperationException(
                    "Exactly one of EmployeeId or ClientEntityId must be provided.");
            }

            // 4. Validate the owner exists
            if (request.EmployeeId.HasValue)
            {
                var employeeExists =
                    await _context.Employees.AnyAsync(
                        x =>
                            x.Id == request.EmployeeId.Value &&
                            !x.IsDeleted,
                        cancellationToken);

                if (!employeeExists)
                {
                    throw new KeyNotFoundException(
                        "Employee was not found.");
                }
            }

            if (request.ClientEntityId.HasValue)
            {
                var entityExists =
                    await _context.ClientEntities.AnyAsync(
                        x =>
                            x.Id == request.ClientEntityId.Value &&
                            !x.IsDeleted,
                        cancellationToken);

                if (!entityExists)
                {
                    throw new KeyNotFoundException(
                        "Client entity was not found.");
                }
            }

            // 5. Validate dates
            if (request.ExpiryDate.HasValue &&
                request.IssueDate.HasValue &&
                request.ExpiryDate.Value <
                request.IssueDate.Value)
            {
                throw new InvalidOperationException(
                    "Expiry date must be greater than or equal to issue date.");
            }

            // 6. Reject when the same owner already holds an active document
            //    with this type and document number.
            await DocumentDuplicateGuard.EnsureNotDuplicateAsync(
                _context,
                request.EmployeeId,
                request.ClientEntityId,
                request.Type,
                request.DocumentNumber,
                cancellationToken);

            // 7. Create the real document.
            //    The server-generated extraction reference is authoritative. Only
            //    legacy extractions without a stored reference fall back to the
            //    client-supplied file location (pre-storage behavior).
            var serverReference = extraction.FileUrl;

            var document = new Document
            {
                Id = Guid.NewGuid(),

                EmployeeId =
                    request.EmployeeId,

                ClientEntityId =
                    request.ClientEntityId,

                Type =
                    request.Type,

                DocumentNumber =
                    request.DocumentNumber.Trim(),

                IssueDate =
                    request.IssueDate,

                ExpiryDate =
                    request.ExpiryDate,

                FileUrl =
                    serverReference ??
                    request.FileUrl?.Trim(),

                FileName =
                    serverReference is null
                        ? request.FileName?.Trim()
                        : extraction.FileName?.Trim() ??
                          request.FileName?.Trim(),

                ContentType =
                    serverReference is null
                        ? request.ContentType?.Trim()
                        : extraction.ContentType?.Trim() ??
                          request.ContentType?.Trim(),

                FileSize =
                    serverReference is null
                        ? request.FileSize
                        : extraction.FileSize,

                Details =
                    DetailsJson.ToStorage(request.Details),

                IsActive = true,

                IsDeleted = false,

                DeletedAt = null,

                CreatedAt = now,

                UpdatedAt = now
            };

            _context.Documents.Add(document);

            // 8. Mark extraction as confirmed
            extraction.Status = "Confirmed";
            extraction.UpdatedAt = now;

            // 9. Record confirmation + created document
            var confirmationAudit = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = _currentUser.UserId,
                Action = "DocumentExtractionConfirmed",
                EntityType = "DocumentExtraction",
                EntityId = extraction.Id,
                Description =
                    "Document extraction was confirmed.",
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            };
            _context.AuditLogs.Add(confirmationAudit);

            var createdAudit = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = _currentUser.UserId,
                Action = "DocumentCreated",
                EntityType = "Document",
                EntityId = document.Id,
                Description =
                    $"Document '{document.FileName ?? document.DocumentNumber}' was created.",
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            };
            _context.AuditLogs.Add(createdAudit);

            // 10. Save changes
            await _context.SaveChangesAsync(
                cancellationToken);

            // 11. Commit: document + extraction status + audits atomically.
            await transaction.CommitAsync(cancellationToken);
            committed = true;

            // 12. Return created document
            return new ConfirmDocumentResponseDto(
                document.Id,
                extraction.Id,
                document.EmployeeId,
                document.ClientEntityId,
                document.Type,
                document.DocumentNumber,
                document.IssueDate,
                document.ExpiryDate,
                document.FileUrl,
                document.FileName,
                document.ContentType,
                document.FileSize,
                document.IsActive,
                document.CreatedAt,
                DetailsJson.FromStorage(document.Details));
        }
        catch
        {
            if (!committed)
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);
            }

            throw;
        }
    }
}