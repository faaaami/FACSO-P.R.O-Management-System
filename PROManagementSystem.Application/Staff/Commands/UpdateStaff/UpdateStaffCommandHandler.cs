using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Staff.Commands.UpdateStaff;

public sealed class UpdateStaffCommandHandler
    : IRequestHandler<
        UpdateStaffCommand,
        UpdateStaffResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateStaffCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<UpdateStaffResponseDto> Handle(
        UpdateStaffCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find staff
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

        // 2. Normalize email
        var normalizedEmail =
            request.Email.Trim().ToLowerInvariant();

        // 3. Check duplicate email
        var emailExists =
            await _context.Users.AnyAsync(
                x =>
                    x.Id != request.StaffId &&
                    x.Email.ToLower() == normalizedEmail &&
                    !x.IsDeleted,
                cancellationToken);

        if (emailExists)
        {
            throw new InvalidOperationException(
                "A user with this email already exists.");
        }

        // 4. Update staff details
        staff.FullName =
            request.FullName.Trim();

        staff.Email =
            normalizedEmail;

        staff.Phone =
            request.Phone?.Trim();

        staff.IsApproved =
            request.IsApproved;

        // IMPORTANT:
        // Role remains UserRole.Agent.

        staff.UpdatedAt =
            DateTime.UtcNow;

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "StaffUpdated",
            EntityType = "User",
            EntityId = staff.Id,
            Description =
                $"Staff member '{staff.FullName}' was updated.",
            CreatedAt = staff.UpdatedAt,
            UpdatedAt = staff.UpdatedAt,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new UpdateStaffResponseDto(
            staff.Id,
            staff.FullName,
            staff.Email,
            staff.Phone,
            staff.Role,
            staff.IsActive,
            staff.IsApproved,
            staff.EmailVerified,
            staff.UpdatedAt);
    }
}