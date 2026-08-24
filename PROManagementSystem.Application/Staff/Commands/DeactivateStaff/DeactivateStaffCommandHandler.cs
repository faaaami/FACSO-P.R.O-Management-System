using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Staff.Commands.DeactivateStaff;

public sealed class DeactivateStaffCommandHandler
    : IRequestHandler<
        DeactivateStaffCommand,
        DeactivateStaffResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeactivateStaffCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<DeactivateStaffResponseDto> Handle(
        DeactivateStaffCommand request,
        CancellationToken cancellationToken)
    {
        var staff =
            await _context.Users
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == request.StaffId &&
                        x.Role == UserRole.Agent &&
                        !x.IsDeleted,
                    cancellationToken);

        if (staff is null)
        {
            throw new KeyNotFoundException(
                "Staff member was not found.");
        }

        if (!staff.IsActive)
        {
            throw new InvalidOperationException(
                "Staff member is already inactive.");
        }

        // Deactivate only.
        // Do NOT soft-delete the User.
        staff.IsActive = false;
        staff.UpdatedAt = DateTime.UtcNow;

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "StaffDeactivated",
            EntityType = "User",
            EntityId = staff.Id,
            Description =
                $"Staff member '{staff.FullName}' was deactivated.",
            CreatedAt = staff.UpdatedAt,
            UpdatedAt = staff.UpdatedAt,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new DeactivateStaffResponseDto(
            staff.Id,
            staff.FullName,
            staff.Email,
            staff.IsActive,
            staff.UpdatedAt);
    }
}