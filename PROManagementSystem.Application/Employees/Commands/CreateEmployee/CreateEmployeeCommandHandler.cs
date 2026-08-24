using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Employees.Commands.CreateEmployee;

public sealed class CreateEmployeeCommandHandler
    : IRequestHandler<
        CreateEmployeeCommand,
        CreateEmployeeResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateEmployeeCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CreateEmployeeResponseDto> Handle(
        CreateEmployeeCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Verify entity exists
        var entityExists =
            await _context.ClientEntities
                .AnyAsync(
                    x => x.Id == request.ClientEntityId,
                    cancellationToken);

        if (!entityExists)
        {
            throw new KeyNotFoundException(
                "Client entity was not found.");
        }

        // 2. Create employee
        var employee = new Employee
        {
            Id = Guid.NewGuid(),

            ClientEntityId =
                request.ClientEntityId,

            FullName =
                request.FullName.Trim(),

            PassportNumber =
                request.PassportNumber?.Trim(),

            DateOfBirth =
                request.DateOfBirth,

            Nationality =
                request.Nationality?.Trim(),

            JobTitle =
                request.JobTitle?.Trim(),

            HireDate =
                request.HireDate,

            IsActive = true,

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,

            IsDeleted = false,
            DeletedAt = null
        };

        _context.Employees.Add(employee);

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),

            UserId = _currentUser.UserId,

            Action = "EmployeeCreated",

            EntityType = "Employee",

            EntityId = employee.Id,

            EmployeeId = employee.Id,

            Description =
                $"Employee '{employee.FullName}' was created.",

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,

            IsDeleted = false,
            DeletedAt = null
        };

        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        // 3. Return response
        return new CreateEmployeeResponseDto(
            employee.Id,
            employee.ClientEntityId,
            employee.FullName,
            employee.PassportNumber,
            employee.DateOfBirth,
            employee.Nationality,
            employee.JobTitle,
            employee.HireDate,
            employee.IsActive);
    }
}