using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Employees.Commands.CreateEmployeeDocument;

public sealed class CreateEmployeeDocumentCommandHandler
    : IRequestHandler<
        CreateEmployeeDocumentCommand,
        CreateEmployeeDocumentResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateEmployeeDocumentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CreateEmployeeDocumentResponseDto> Handle(
        CreateEmployeeDocumentCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Verify employee exists
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

        // 2. Do not allow documents to be added
        // to a soft-deleted employee.
        if (employee.IsDeleted)
        {
            throw new InvalidOperationException(
                "Cannot add a document to a deleted employee.");
        }

        // 3. Create document
        var now = DateTime.UtcNow;

        var document = new Document
        {
            Id = Guid.NewGuid(),

            EmployeeId = employee.Id,

            ClientEntityId = null,

            Type = request.Type,

            DocumentNumber =
                request.DocumentNumber.Trim(),

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

        // 4. Create audit log
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

        // 5. Save document + audit log together
        await _context.SaveChangesAsync(
            cancellationToken);

        // 6. Return response
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