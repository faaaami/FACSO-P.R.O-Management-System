using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Employees.Commands.DeleteEmployee;

public sealed class DeleteEmployeeCommandHandler
    : IRequestHandler<
        DeleteEmployeeCommand,
        DeleteEmployeeResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteEmployeeCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<DeleteEmployeeResponseDto> Handle(
        DeleteEmployeeCommand request,
        CancellationToken cancellationToken)
    {
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

        var deletedAt = DateTime.UtcNow;

        employee.IsDeleted = true;
        employee.DeletedAt = deletedAt;
        employee.IsActive = false;
        employee.UpdatedAt = deletedAt;

        // Create audit log
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),

            UserId = _currentUser.UserId,

            Action = "EmployeeDeleted",

            EntityType = "Employee",

            EntityId = employee.Id,

            EmployeeId = employee.Id,

            Description =
                $"Employee '{employee.FullName}' was deleted.",

            CreatedAt = deletedAt,
            UpdatedAt = deletedAt,

            IsDeleted = false,
            DeletedAt = null
        };

        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new DeleteEmployeeResponseDto(
            employee.Id,
            employee.IsDeleted,
            employee.IsActive,
            deletedAt);
    }
}