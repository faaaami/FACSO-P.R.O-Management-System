using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Auth.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandHandler
    : IRequestHandler<ForgotPasswordCommand, ForgotPasswordResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;

    public ForgotPasswordCommandHandler(
        IApplicationDbContext context,
        IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    public async Task<ForgotPasswordResponseDto> Handle(
        ForgotPasswordCommand request,
        CancellationToken cancellationToken)
    {
        const string message =
            "If an account exists with this email, a password reset link has been sent.";

        // 1. Find the user
        var user = await _context.Users
            .FirstOrDefaultAsync(
                x => x.Email == request.Email.Trim().ToLowerInvariant(),
                cancellationToken);

        // 2. Do not reveal whether the email exists
        if (user is null)
        {
            return new ForgotPasswordResponseDto(message);
        }

        // 3. Generate a secure random token
        var tokenBytes = RandomNumberGenerator.GetBytes(32);

        var resetToken =
            Convert.ToBase64String(tokenBytes);

        // 4. Hash the token before storing it
        var tokenHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(resetToken)));

        // 5. Create reset token entity
        var resetTokenEntity = new PasswordResetToken
        {
            Id = Guid.NewGuid(),

            UserId = user.Id,

            TokenHash = tokenHash,

            ExpiresAt = DateTime.UtcNow.AddHours(1),

            CreatedAt = DateTime.UtcNow,

            UpdatedAt = DateTime.UtcNow
        };

        _context.PasswordResetTokens.Add(
            resetTokenEntity);

        // 6. Save the token
        await _context.SaveChangesAsync(
            cancellationToken);

        // 7. Send reset token through email service
        await _emailService.SendPasswordResetAsync(
            user.Email,
            resetToken,
            cancellationToken);

        // 8. Return generic response
        return new ForgotPasswordResponseDto(message);
    }
}