using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Documents.Commands.UpdateDocument;

public sealed class UpdateDocumentCommandHandler
    : IRequestHandler<
        UpdateDocumentCommand,
        UpdateDocumentResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateDocumentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<UpdateDocumentResponseDto> Handle(
        UpdateDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var document =
            await _context.Documents
                .FirstOrDefaultAsync(
                    x => x.Id == request.DocumentId,
                    cancellationToken);

        if (document is null)
        {
            throw new KeyNotFoundException(
                "Document was not found.");
        }

        if (document.IsDeleted)
        {
            throw new InvalidOperationException(
                "Cannot update a deleted document.");
        }

        if (request.ExpiryDate.HasValue &&
            request.IssueDate.HasValue &&
            request.ExpiryDate.Value < request.IssueDate.Value)
        {
            throw new InvalidOperationException(
                "Expiry date must be greater than or equal to issue date.");
        }

        // Task 7B.6.4: the update may change Type and/or DocumentNumber, so the
        // resulting owner/type/number combination must not collide with an
        // existing active document. The document being updated is excluded so a
        // no-op update of the same values remains valid.
        await DocumentDuplicateGuard.EnsureNotDuplicateAsync(
            _context,
            document.EmployeeId,
            document.ClientEntityId,
            request.Type,
            request.DocumentNumber,
            cancellationToken,
            document.Id);

        document.Type = request.Type;

        document.DocumentNumber =
            request.DocumentNumber.Trim();

        document.IssueDate =
            request.IssueDate;

        document.ExpiryDate =
            request.ExpiryDate;

        document.FileUrl =
            request.FileUrl?.Trim();

        document.FileName =
            request.FileName?.Trim();

        document.ContentType =
            request.ContentType?.Trim();

        document.FileSize =
            request.FileSize;

        document.IsActive =
            request.IsActive;

        // A supplied (non-null) Details object replaces the stored value.
        // Omitted and explicitly-null Details preserve the existing value.
        if (request.Details is not null)
        {
            document.Details =
                DetailsJson.ToStorage(request.Details);
        }

        document.UpdatedAt =
            DateTime.UtcNow;

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "DocumentUpdated",
            EntityType = "Document",
            EntityId = document.Id,
            Description =
                $"Document '{document.FileName ?? document.DocumentNumber}' was updated.",
            CreatedAt = document.UpdatedAt,
            UpdatedAt = document.UpdatedAt,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new UpdateDocumentResponseDto(
            document.Id,
            document.ClientEntityId,
            document.EmployeeId,
            document.Type,
            document.DocumentNumber,
            document.IssueDate,
            document.ExpiryDate,
            document.FileUrl,
            document.FileName,
            document.ContentType,
            document.FileSize,
            document.IsActive,
            document.UpdatedAt,
            DetailsJson.FromStorage(document.Details));
    }
}