using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Auth.Commands.VerifyEmail;

public sealed class VerifyEmailCommandHandler
    : IRequestHandler<VerifyEmailCommand, VerifyEmailResponseDto>
{
    private readonly IApplicationDbContext _context;

    public VerifyEmailCommandHandler(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<VerifyEmailResponseDto> Handle(
        VerifyEmailCommand request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var tokenHash = AccountTokens.Hash(request.Token.Trim());

        await using var transaction =
            await _context.BeginTransactionAsync(cancellationToken);

        var storedToken = await _context.EmailVerificationTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(
                x => x.TokenHash == tokenHash,
                cancellationToken);

        if (storedToken is null ||
            storedToken.User.IsDeleted ||
            !storedToken.User.IsActive)
        {
            throw new UnauthorizedAccessException(
                "This verification link is invalid. Please request a new one.");
        }

        if (storedToken.UsedAt.HasValue)
        {
            throw new UnauthorizedAccessException(
                "This verification link has already been used.");
        }

        if (storedToken.ExpiresAt <= now)
        {
            throw new UnauthorizedAccessException(
                "This verification link has expired. Please request a new one.");
        }

        var claimed = await _context.EmailVerificationTokens
            .Where(x => x.Id == storedToken.Id && x.UsedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.UsedAt, now)
                    .SetProperty(x => x.UpdatedAt, now),
                cancellationToken);

        if (claimed == 0)
        {
            throw new UnauthorizedAccessException(
                "This verification link has already been used.");
        }

        var user = storedToken.User;
        user.EmailVerified = true;
        user.UpdatedAt = now;

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = null,
            Action = "EmailVerified",
            EntityType = "User",
            EntityId = user.Id,
            Description = "User email was verified.",
            CreatedAt = now,
            UpdatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new VerifyEmailResponseDto(
            user.Id,
            user.Email,
            user.EmailVerified);
    }
}
