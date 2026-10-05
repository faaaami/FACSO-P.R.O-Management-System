using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Auth.Commands.Logout;

public sealed class LogoutCommandHandler
    : IRequestHandler<LogoutCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ITokenService _tokenService;

    public LogoutCommandHandler(
        IApplicationDbContext context,
        ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    public async Task Handle(
        LogoutCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find the user using the hash of the refresh token
        var refreshTokenHash =
            _tokenService.HashRefreshToken(request.RefreshToken);

        var user = await _context.Users
            .FirstOrDefaultAsync(
                x => x.RefreshToken == refreshTokenHash,
                cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException(
                "Invalid refresh token.");
        }

        // 2. Validate the refresh token
        var isValid = _tokenService.ValidateRefreshToken(
            user,
            request.RefreshToken);

        if (!isValid)
        {
            throw new UnauthorizedAccessException(
                "Invalid or expired refresh token.");
        }

        // 3. Revoke the refresh token
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;

        // 4. Record the logout
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = "Logout",
            EntityType = "User",
            EntityId = user.Id,
            Description =
                "User logged out.",
            CreatedAt = user.UpdatedAt,
            UpdatedAt = user.UpdatedAt,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        // 5. Save changes
        await _context.SaveChangesAsync(
            cancellationToken);
    }
}