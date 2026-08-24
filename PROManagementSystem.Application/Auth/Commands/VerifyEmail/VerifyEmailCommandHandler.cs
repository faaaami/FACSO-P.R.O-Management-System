using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

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
        // 1. Hash the token received from the client
        var tokenHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(request.Token)));

        // 2. Find the verification token
        var storedToken = await _context.EmailVerificationTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(
                verification => verification.TokenHash == tokenHash,
                cancellationToken);

        // 3. Check whether token exists
        if (storedToken is null)
        {
            throw new UnauthorizedAccessException(
                "Invalid verification token.");
        }

        // 4. Check whether token was already used
        if (storedToken.UsedAt.HasValue)
        {
            throw new UnauthorizedAccessException(
                "Verification token has already been used.");
        }

        // 5. Check whether token has expired
        if (storedToken.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException(
                "Verification token has expired.");
        }

        // 6. Verify the user's email
        storedToken.User.EmailVerified = true;

        // 7. Mark the token as used
        storedToken.UsedAt = DateTime.UtcNow;

        // 8. Update timestamps
        storedToken.User.UpdatedAt = DateTime.UtcNow;
        storedToken.UpdatedAt = DateTime.UtcNow;

        // 9. Save changes
        await _context.SaveChangesAsync(
            cancellationToken);

        // 10. Return DTO
        return new VerifyEmailResponseDto(
            storedToken.User.Id,
            storedToken.User.Email,
            storedToken.User.EmailVerified);
    }
}