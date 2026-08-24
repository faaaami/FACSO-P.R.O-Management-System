using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Staff.Commands.CreateStaff;

public sealed class CreateStaffCommandHandler
    : IRequestHandler<
        CreateStaffCommand,
        CreateStaffResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUser;

    public CreateStaffCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ICurrentUserService currentUser)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
    }

    public async Task<CreateStaffResponseDto> Handle(
        CreateStaffCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Normalize email
        var normalizedEmail =
            request.Email.Trim().ToLowerInvariant();

        // 2. Check duplicate email
        var emailExists =
            await _context.Users.AnyAsync(
                x =>
                    x.Email.ToLower() == normalizedEmail &&
                    !x.IsDeleted,
                cancellationToken);

        if (emailExists)
        {
            throw new InvalidOperationException(
                "A user with this email already exists.");
        }

        // 3. Validate password
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidOperationException(
                "Password is required.");
        }

        // 4. Create Agent
        var now = DateTime.UtcNow;

        var staff = new User
        {
            Id = Guid.NewGuid(),

            FullName =
                request.FullName.Trim(),

            Email =
                normalizedEmail,

            PasswordHash =
                _passwordHasher.Hash(
                    request.Password),

            Phone =
                request.Phone?.Trim(),

            // Staff = Agent in your system
            Role =
                UserRole.Agent,

            IsActive = true,

            IsApproved = true,

            EmailVerified = false,

            ClientCompanyId = null,

            CreatedAt = now,

            UpdatedAt = now,

            IsDeleted = false,

            DeletedAt = null
        };

        _context.Users.Add(staff);

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "StaffCreated",
            EntityType = "User",
            EntityId = staff.Id,
            Description =
                $"Staff member '{staff.FullName}' was created.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new CreateStaffResponseDto(
            staff.Id,
            staff.FullName,
            staff.Email,
            staff.Phone,
            staff.Role,
            staff.IsActive,
            staff.IsApproved,
            staff.EmailVerified,
            staff.CreatedAt);
    }
}