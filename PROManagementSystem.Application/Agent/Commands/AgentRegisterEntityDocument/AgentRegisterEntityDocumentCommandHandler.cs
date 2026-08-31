using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Clients.Commands.CreateClientEntityDocument;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Agent.Commands.AgentRegisterEntityDocument;

public sealed class AgentRegisterEntityDocumentCommandHandler
    : IRequestHandler<
        AgentRegisterEntityDocumentCommand,
        CreateClientEntityDocumentResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRenewalTaskReadRepository _scopeRepository;

    public AgentRegisterEntityDocumentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IRenewalTaskReadRepository scopeRepository)
    {
        _context = context;
        _currentUser = currentUser;
        _scopeRepository = scopeRepository;
    }

    public async Task<CreateClientEntityDocumentResponseDto> Handle(
        AgentRegisterEntityDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        // 1. Verify entity exists and is within agent scope
        var entity =
            await _context.ClientEntities
                .FirstOrDefaultAsync(
                    x => x.Id == request.EntityId,
                    cancellationToken);

        if (entity is null)
        {
            throw new KeyNotFoundException(
                "Client entity was not found.");
        }

        if (entity.IsDeleted)
        {
            throw new InvalidOperationException(
                "Cannot add a document to a deleted entity.");
        }

        var entityInScope =
            await _scopeRepository.IsAgentEntityInScopeAsync(
                agentId,
                request.EntityId,
                cancellationToken);

        if (!entityInScope)
        {
            throw new KeyNotFoundException(
                "Client entity was not found.");
        }

        // 2. Create document
        var now = DateTime.UtcNow;

        var document = new Document
        {
            Id = Guid.NewGuid(),

            ClientEntityId = entity.Id,

            EmployeeId = null,

            Type = request.Type,

            DocumentNumber = request.DocumentNumber.Trim(),

            IssueDate = request.IssueDate,

            ExpiryDate = request.ExpiryDate,

            FileUrl = request.FileUrl?.Trim(),

            FileName = request.FileName?.Trim(),

            ContentType = request.ContentType?.Trim(),

            FileSize = request.FileSize,

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
            document.CreatedAt);
    }
}
