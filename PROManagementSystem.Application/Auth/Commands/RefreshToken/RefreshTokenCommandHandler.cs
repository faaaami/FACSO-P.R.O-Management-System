using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Auth.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler
    : IRequestHandler<RefreshTokenCommand, RefreshTokenResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ITokenService _tokenService;

    public RefreshTokenCommandHandler(
        IApplicationDbContext context,
        ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    public async Task<RefreshTokenResponseDto> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find the user by the hash of the refresh token
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

        // 3. Make sure the account is still active
        if (!user.IsActive)
        {
            throw new ForbiddenAccessException(
                "Your account is inactive.");
        }

        if (!user.IsApproved)
        {
            throw new ForbiddenAccessException(
                "Your account has not been approved yet.");
        }

        if (!user.EmailVerified)
        {
            throw new ForbiddenAccessException(
                "Please verify your email before logging in.");
        }

        // 4. Generate a new access token
        var accessToken = _tokenService.GenerateAccessToken(user);

        // 5. Generate a new refresh token
        var newRefreshToken =
            _tokenService.GenerateRefreshToken();

        // 6. Rotate the refresh token (store only the hash)
        user.RefreshToken =
            _tokenService.HashRefreshToken(newRefreshToken);

        user.RefreshTokenExpiry =
            DateTime.UtcNow.Add(_tokenService.RefreshTokenLifetime);

        user.UpdatedAt = DateTime.UtcNow;

        // 7. Record the refresh
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = "RefreshTokenIssued",
            EntityType = "User",
            EntityId = user.Id,
            Description =
                "New refresh token issued.",
            CreatedAt = user.UpdatedAt,
            UpdatedAt = user.UpdatedAt,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        // 8. Save the new refresh token
        await _context.SaveChangesAsync(
            cancellationToken);

        // 9. Return the new token pair
        return new RefreshTokenResponseDto(
            accessToken,
            newRefreshToken,
            (int)_tokenService.AccessTokenLifetime.TotalSeconds,
            user.MustChangePassword);
    }
}