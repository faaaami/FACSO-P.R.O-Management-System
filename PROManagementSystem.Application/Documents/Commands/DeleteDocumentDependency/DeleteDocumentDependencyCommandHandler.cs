using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Documents.Commands.DeleteDocumentDependency;

public sealed class DeleteDocumentDependencyCommandHandler
    : IRequestHandler<
        DeleteDocumentDependencyCommand,
        DeleteDocumentDependencyResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteDocumentDependencyCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<DeleteDocumentDependencyResponseDto> Handle(
        DeleteDocumentDependencyCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find the dependency
        var dependency =
            await _context.DocumentDependencies
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == request.DependencyId &&
                        x.DocumentId == request.DocumentId,
                    cancellationToken);

        if (dependency is null)
        {
            throw new KeyNotFoundException(
                "Document dependency was not found.");
        }

        // 2. Prevent deleting an already deleted dependency
        if (dependency.IsDeleted)
        {
            throw new InvalidOperationException(
                "Document dependency has already been deleted.");
        }

        // 3. Soft delete
        var now = DateTime.UtcNow;

        dependency.IsDeleted = true;
        dependency.DeletedAt = now;
        dependency.UpdatedAt = now;

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "DocumentDependencyDeleted",
            EntityType = "DocumentDependency",
            EntityId = dependency.Id,
            Description =
                $"Document '{dependency.DocumentId}' no longer depends " +
                $"on document '{dependency.DependsOnDocumentId}'.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        // 4. Return response
        return new DeleteDocumentDependencyResponseDto(
            dependency.Id,
            dependency.IsDeleted,
            dependency.DeletedAt!.Value);
    }
}