using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Users.Commands.ChangePassword;

public sealed class ChangePasswordCommandHandler
    : IRequestHandler<ChangePasswordCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;

    public ChangePasswordCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
    }

    public async Task Handle(
        ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new ForbiddenAccessException(
                "Authenticated user was not found.");
        }

        var user =
            await _context.Users
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == _currentUser.UserId.Value &&
                        !x.IsDeleted,
                    cancellationToken);

        if (user is null)
        {
            throw new NotFoundException(
                "User was not found.");
        }

        var isCurrentPasswordValid =
            _passwordHasher.Verify(
                request.CurrentPassword,
                user.PasswordHash);

        if (!isCurrentPasswordValid)
        {
            throw new ConflictException(
                "Current password is incorrect.");
        }

        var newPasswordHash =
            _passwordHasher.Hash(request.NewPassword);

        user.PasswordHash = newPasswordHash;
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "PasswordChanged",
            EntityType = "User",
            EntityId = user.Id,
            Description = "User changed their password.",
            CreatedAt = user.UpdatedAt,
            UpdatedAt = user.UpdatedAt,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);
    }
}
