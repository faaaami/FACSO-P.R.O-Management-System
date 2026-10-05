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

        // 2. Normalize email, but ONLY when the caller supplied one.
        // PATCH semantics: an omitted field is "leave this alone", not
        // "write null" and not "write false".
        string? normalizedEmail = null;

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            normalizedEmail =
                request.Email.Trim().ToLowerInvariant();

            // 3. Check duplicate email (only meaningful when supplied)
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
        }

        // 4. Update staff details (supplied fields only)
        staff.FullName =
            request.FullName.Trim();

        if (normalizedEmail is not null)
        {
            staff.Email =
                normalizedEmail;
        }

        staff.Phone =
            request.Phone?.Trim();

        // IsApproved is written ONLY when explicitly supplied. Writing it
        // unconditionally is what let a partial payload revoke approval.
        if (request.IsApproved.HasValue)
        {
            staff.IsApproved =
                request.IsApproved.Value;
        }

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