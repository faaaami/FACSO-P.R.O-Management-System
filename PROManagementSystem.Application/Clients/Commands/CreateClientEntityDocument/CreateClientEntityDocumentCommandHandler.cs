using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Clients.Commands.CreateClientEntityDocument;

public sealed class CreateClientEntityDocumentCommandHandler
    : IRequestHandler<
        CreateClientEntityDocumentCommand,
        CreateClientEntityDocumentResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateClientEntityDocumentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CreateClientEntityDocumentResponseDto> Handle(
        CreateClientEntityDocumentCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Verify entity exists
        var entity =
            await _context.ClientEntities
                .FirstOrDefaultAsync(
                    x => x.Id == request.ClientEntityId,
                    cancellationToken);

        if (entity is null)
        {
            throw new KeyNotFoundException(
                "Client entity was not found.");
        }

        // 2. Do not allow documents to be added
        // to a deleted entity.
        if (entity.IsDeleted)
        {
            throw new InvalidOperationException(
                "Cannot add a document to a deleted entity.");
        }

        // 3. Reject when the same owner already holds an active document
        //    with this type and document number.
        await DocumentDuplicateGuard.EnsureNotDuplicateAsync(
            _context,
            null,
            entity.Id,
            request.Type,
            request.DocumentNumber,
            cancellationToken);

        // 4. Create document
        var now = DateTime.UtcNow;

        var document = new Document
        {
            Id = Guid.NewGuid(),

            ClientEntityId = entity.Id,

            EmployeeId = null,

            Type = request.Type,

            DocumentNumber =
                request.DocumentNumber.Trim(),

            IssueDate = request.IssueDate,

            ExpiryDate = request.ExpiryDate,

            FileUrl = request.FileUrl?.Trim(),

            FileName = request.FileName?.Trim(),

            ContentType = request.ContentType?.Trim(),

            FileSize = request.FileSize,

            Details =
                DetailsJson.ToStorage(request.Details),

            IsActive = true,

            CreatedAt = now,

            UpdatedAt = now,

            IsDeleted = false,

            DeletedAt = null
        };

        _context.Documents.Add(document);

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ClientEntityDocumentCreated",
            EntityType = "Document",
            EntityId = document.Id,
            EmployeeId = null,
            Description =
                $"Document '{document.FileName ?? document.DocumentNumber}' was created for entity '{entity.EntityName}'.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new CreateClientEntityDocumentResponseDto(
            document.Id,
            document.ClientEntityId!.Value,
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
}