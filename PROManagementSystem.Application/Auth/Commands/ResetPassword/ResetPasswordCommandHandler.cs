using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Auth.Commands.ResetPassword;

public sealed class ResetPasswordCommandHandler
    : IRequestHandler<ResetPasswordCommand, ResetPasswordResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public ResetPasswordCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<ResetPasswordResponseDto> Handle(
        ResetPasswordCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Hash the token received from the client
        var tokenHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(request.Token)));

        // 2. Find the password reset token
        var resetToken = await _context.PasswordResetTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(
                x => x.TokenHash == tokenHash,
                cancellationToken);

        // 3. Validate token exists
        if (resetToken is null)
        {
            throw new UnauthorizedAccessException(
                "Invalid password reset token.");
        }

        // 4. Make sure token hasn't already been used
        if (resetToken.UsedAt.HasValue)
        {
            throw new UnauthorizedAccessException(
                "Password reset token has already been used.");
        }

        // 5. Make sure token hasn't expired
        if (resetToken.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException(
                "Password reset token has expired.");
        }

        // 6. Hash the new password
        var passwordHash = _passwordHasher.Hash(
            request.NewPassword);

        // 7. Update user's password
        resetToken.User.PasswordHash = passwordHash;

        // 8. Mark reset token as used
        resetToken.UsedAt = DateTime.UtcNow;

        // 9. Update timestamps
        resetToken.User.UpdatedAt = DateTime.UtcNow;
        resetToken.UpdatedAt = DateTime.UtcNow;

        // 10. Revoke existing refresh token
        resetToken.User.RefreshToken = null;
        resetToken.User.RefreshTokenExpiry = null;

        // 11. Record the password reset
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = null,
            Action = "PasswordReset",
            EntityType = "User",
            EntityId = resetToken.User.Id,
            Description =
                "User password was reset.",
            CreatedAt = resetToken.User.UpdatedAt,
            UpdatedAt = resetToken.User.UpdatedAt,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        // 12. Save changes
        await _context.SaveChangesAsync(
            cancellationToken);

        // 13. Return response
        return new ResetPasswordResponseDto(
            "Password has been reset successfully.");
    }
}