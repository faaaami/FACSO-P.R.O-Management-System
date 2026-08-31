using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Employees.Commands.CreateEmployeeDocument;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Agent.Commands.AgentRegisterEmployeeDocument;

public sealed class AgentRegisterEmployeeDocumentCommandHandler
    : IRequestHandler<
        AgentRegisterEmployeeDocumentCommand,
        CreateEmployeeDocumentResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRenewalTaskReadRepository _scopeRepository;

    public AgentRegisterEmployeeDocumentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IRenewalTaskReadRepository scopeRepository)
    {
        _context = context;
        _currentUser = currentUser;
        _scopeRepository = scopeRepository;
    }

    public async Task<CreateEmployeeDocumentResponseDto> Handle(
        AgentRegisterEmployeeDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

        // 1. Verify employee exists and is within agent scope
        var employee =
            await _context.Employees
                .FirstOrDefaultAsync(
                    x => x.Id == request.EmployeeId,
                    cancellationToken);

        if (employee is null)
        {
            throw new KeyNotFoundException(
                "Employee was not found.");
        }

        if (employee.IsDeleted)
        {
            throw new InvalidOperationException(
                "Cannot add a document to a deleted employee.");
        }

        var employeeInScope =
            await _scopeRepository.IsAgentEmployeeInScopeAsync(
                agentId,
                request.EmployeeId,
                cancellationToken);

        if (!employeeInScope)
        {
            throw new KeyNotFoundException(
                "Employee was not found.");
        }

        // 2. Create document
        var now = DateTime.UtcNow;

        var document = new Document
        {
            Id = Guid.NewGuid(),

            EmployeeId = employee.Id,

            ClientEntityId = null,

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

        // 3. Create audit log
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),

            UserId = _currentUser.UserId,

            Action = "DocumentCreated",

            EntityType = "EmployeeDocument",

            EntityId = document.Id,

            EmployeeId = employee.Id,

            Description =
                $"Document '{document.FileName ?? document.DocumentNumber}' was created for employee '{employee.FullName}'.",

            CreatedAt = now,

            UpdatedAt = now,

            IsDeleted = false,

            DeletedAt = null
        };

        _context.AuditLogs.Add(auditLog);

        // 4. Save
        await _context.SaveChangesAsync(cancellationToken);

        // 5. Return response
        return new CreateEmployeeDocumentResponseDto(
            document.Id,
            document.EmployeeId!.Value,
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
