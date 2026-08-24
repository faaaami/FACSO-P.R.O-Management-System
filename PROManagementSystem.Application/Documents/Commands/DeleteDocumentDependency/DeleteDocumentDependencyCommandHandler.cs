using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Documents.Commands.DeleteDocumentDependency;

public sealed class DeleteDocumentDependencyCommandHandler
    : IRequestHandler<
        DeleteDocumentDependencyCommand,
        DeleteDocumentDependencyResponseDto>
{
    private readonly IApplicationDbContext _context;

    public DeleteDocumentDependencyCommandHandler(
        IApplicationDbContext context)
    {
        _context = context;
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

        await _context.SaveChangesAsync(
            cancellationToken);

        // 4. Return response
        return new DeleteDocumentDependencyResponseDto(
            dependency.Id,
            dependency.IsDeleted,
            dependency.DeletedAt!.Value);
    }
}