using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Commands.CreateDocumentVersion;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Agent.Commands.AgentCreateDocumentVersion;

public sealed class AgentCreateDocumentVersionCommandHandler
    : IRequestHandler<
        AgentCreateDocumentVersionCommand,
        CreateDocumentVersionResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRenewalTaskReadRepository _scopeRepository;

    public AgentCreateDocumentVersionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IRenewalTaskReadRepository scopeRepository)
    {
        _context = context;
        _currentUser = currentUser;
        _scopeRepository = scopeRepository;
    }

    public async Task<CreateDocumentVersionResponseDto> Handle(
        AgentCreateDocumentVersionCommand request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

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

        if (document.IsDeleted)
        {
            throw new InvalidOperationException(
                "Cannot create a version for a deleted document.");
        }

        // 2. Strict task-linked scope check. Out-of-scope -> 404.
        var documentInScope =
            await _scopeRepository.IsAgentDocumentInScopeAsync(
                agentId,
                request.DocumentId,
                cancellationToken);

        if (!documentInScope)
        {
            throw new KeyNotFoundException(
                "Document was not found.");
        }

        // 3. Find the latest version number
        var latestVersion =
            await _context.DocumentVersions
                .Where(x =>
                    x.DocumentId == request.DocumentId &&
                    !x.IsDeleted)
                .MaxAsync(
                    x => (int?)x.VersionNumber,
                    cancellationToken);

        var nextVersionNumber = (latestVersion ?? 0) + 1;

        var now = DateTime.UtcNow;

        // 4. Create new version
        var version = new DocumentVersion
        {
            Id = Guid.NewGuid(),

            DocumentId = document.Id,

            VersionNumber = nextVersionNumber,

            FileUrl = request.FileUrl.Trim(),

            FileName = request.FileName?.Trim(),

            ContentType = request.ContentType?.Trim(),

            FileSize = request.FileSize,

            CreatedAt = now,

            UpdatedAt = now,

            IsDeleted = false,

            DeletedAt = null
        };

        _context.DocumentVersions.Add(version);

        // 5. Update current document file
        document.FileUrl = request.FileUrl.Trim();
        document.FileName = request.FileName?.Trim();
        document.ContentType = request.ContentType?.Trim();
        document.FileSize = request.FileSize;
        document.UpdatedAt = now;

        // 6. Create audit log
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),

            UserId = _currentUser.UserId,

            Action = "DocumentVersionCreated",

            EntityType = "DocumentVersion",

            EntityId = version.Id,

            EmployeeId = document.EmployeeId,

            Description =
                $"Version {version.VersionNumber} was created for document '{document.FileName ?? document.DocumentNumber}'.",

            CreatedAt = now,

            UpdatedAt = now,

            IsDeleted = false,

            DeletedAt = null
        };

        _context.AuditLogs.Add(auditLog);

        // 7. Save atomically
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateDocumentVersionResponseDto(
            version.Id,
            version.DocumentId,
            version.VersionNumber,
            version.FileUrl,
            version.FileName,
            version.ContentType,
            version.FileSize,
            version.CreatedAt);
    }
}
