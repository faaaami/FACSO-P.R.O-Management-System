using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Documents.Commands.CreateDocumentDependency;

public sealed class CreateDocumentDependencyCommandHandler
    : IRequestHandler<
        CreateDocumentDependencyCommand,
        CreateDocumentDependencyResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateDocumentDependencyCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CreateDocumentDependencyResponseDto> Handle(
        CreateDocumentDependencyCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find the document
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

        // 2. Do not allow deleted documents
        if (document.IsDeleted)
        {
            throw new InvalidOperationException(
                "Cannot create a dependency for a deleted document.");
        }

        // 3. Find the dependency document
        var dependsOnDocument =
            await _context.Documents
                .FirstOrDefaultAsync(
                    x => x.Id == request.DependsOnDocumentId,
                    cancellationToken);

        if (dependsOnDocument is null)
        {
            throw new KeyNotFoundException(
                "Dependency document was not found.");
        }

        // 4. Dependency document must also be active
        if (dependsOnDocument.IsDeleted)
        {
            throw new InvalidOperationException(
                "Cannot depend on a deleted document.");
        }

        // 5. Prevent duplicate dependency
        var alreadyExists =
            await _context.DocumentDependencies
                .AnyAsync(
                    x =>
                        x.DocumentId == request.DocumentId &&
                        x.DependsOnDocumentId ==
                            request.DependsOnDocumentId &&
                        !x.IsDeleted,
                    cancellationToken);

        if (alreadyExists)
        {
            throw new InvalidOperationException(
                "This document dependency already exists.");
        }

        // 6. Create dependency
        var now = DateTime.UtcNow;

        var dependency = new DocumentDependency
        {
            Id = Guid.NewGuid(),

            DocumentId =
                document.Id,

            DependsOnDocumentId =
                dependsOnDocument.Id,

            CreatedAt = now,

            UpdatedAt = now,

            IsDeleted = false,

            DeletedAt = null
        };

        _context.DocumentDependencies.Add(dependency);

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "DocumentDependencyCreated",
            EntityType = "DocumentDependency",
            EntityId = dependency.Id,
            Description =
                $"Document '{dependency.DocumentId}' now depends " +
                $"on document '{dependency.DependsOnDocumentId}'.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        // 7. Return response
        return new CreateDocumentDependencyResponseDto(
            dependency.Id,
            dependency.DocumentId,
            dependency.DependsOnDocumentId,
            dependency.CreatedAt);
    }
}