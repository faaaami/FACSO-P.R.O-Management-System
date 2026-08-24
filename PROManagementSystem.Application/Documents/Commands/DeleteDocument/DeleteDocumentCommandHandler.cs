using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Documents.Commands.DeleteDocument;

public sealed class DeleteDocumentCommandHandler
    : IRequestHandler<
        DeleteDocumentCommand,
        DeleteDocumentResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteDocumentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<DeleteDocumentResponseDto> Handle(
        DeleteDocumentCommand request,
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
                "Document has already been deleted.");
        }

        var now = DateTime.UtcNow;

        document.IsDeleted = true;
        document.IsActive = false;
        document.DeletedAt = now;
        document.UpdatedAt = now;

        // Create audit log
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),

            UserId = _currentUser.UserId,

            Action = "DocumentDeleted",

            EntityType = "EmployeeDocument",

            EntityId = document.Id,

            EmployeeId = document.EmployeeId,

            Description =
                $"Document '{document.FileName ?? document.DocumentNumber}' was deleted.",

            CreatedAt = now,

            UpdatedAt = now,

            IsDeleted = false,

            DeletedAt = null
        };

        _context.AuditLogs.Add(auditLog);

        // Save document deletion + audit log together
        await _context.SaveChangesAsync(
            cancellationToken);

        return new DeleteDocumentResponseDto(
            document.Id,
            document.IsDeleted,
            document.IsActive,
            document.DeletedAt!.Value);
    }
}