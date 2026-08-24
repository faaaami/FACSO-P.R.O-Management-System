using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Employees.Commands.UpdateEmployee;

public sealed class UpdateEmployeeCommandHandler
    : IRequestHandler<
        UpdateEmployeeCommand,
        UpdateEmployeeResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateEmployeeCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<UpdateEmployeeResponseDto> Handle(
        UpdateEmployeeCommand request,
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

        employee.FullName =
            request.FullName.Trim();

        employee.PassportNumber =
            request.PassportNumber?.Trim();

        employee.DateOfBirth =
            request.DateOfBirth;

        employee.Nationality =
            request.Nationality?.Trim();

        employee.JobTitle =
            request.JobTitle?.Trim();

        employee.HireDate =
            request.HireDate;

        employee.IsActive =
            request.IsActive;

        employee.UpdatedAt =
            DateTime.UtcNow;

        // Create audit log
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),

            UserId = _currentUser.UserId,

            Action = "EmployeeUpdated",

            EntityType = "Employee",

            EntityId = employee.Id,

            EmployeeId = employee.Id,

            Description =
                $"Employee '{employee.FullName}' was updated.",

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,

            IsDeleted = false,
            DeletedAt = null
        };

        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new UpdateEmployeeResponseDto(
            employee.Id,
            employee.ClientEntityId,
            employee.FullName,
            employee.PassportNumber,
            employee.DateOfBirth,
            employee.Nationality,
            employee.JobTitle,
            employee.HireDate,
            employee.IsActive,
            employee.UpdatedAt);
    }
}