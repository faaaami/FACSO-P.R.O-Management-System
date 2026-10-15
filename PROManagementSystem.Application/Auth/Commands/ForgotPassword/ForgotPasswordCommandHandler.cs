using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Auth.Common;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Models;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Auth.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandHandler
    : IRequestHandler<ForgotPasswordCommand, ForgotPasswordResponseDto>
{
    public const string GenericMessage =
        "If an account exists with this email, a password reset link has been sent.";

    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly SystemAccountOptions _systemAccounts;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    public ForgotPasswordCommandHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        IOptions<SystemAccountOptions> systemAccounts,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _systemAccounts = systemAccounts.Value;
        _logger = logger;
    }

    public async Task<ForgotPasswordResponseDto> Handle(
        ForgotPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(
                x => x.Email == normalizedEmail &&
                     x.IsActive &&
                     x.Id != _systemAccounts.AutomationUserId,
                cancellationToken);

        // The response never reveals whether the email exists.
        if (user is null)
        {
            return new ForgotPasswordResponseDto(GenericMessage);
        }

        var now = DateTime.UtcNow;
        var rawToken = AccountTokens.Generate();
        var expiresAt = now.Add(AccountTokens.PasswordResetLifetime);

        // Only the newest link works.
        await PasswordTokenRedemption.RevokeOutstandingAsync(
            _context,
            user.Id,
            AccountTokenPurpose.PasswordReset,
            now,
            cancellationToken);

        _context.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = AccountTokens.Hash(rawToken),
            Purpose = AccountTokenPurpose.PasswordReset,
            ExpiresAt = expiresAt,
            CreatedAt = now,
            UpdatedAt = now
        });

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = null,
            Action = "PasswordResetRequested",
            EntityType = "User",
            EntityId = user.Id,
            Description = "A password reset link was requested.",
            CreatedAt = now,
            UpdatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await _emailService.SendPasswordResetAsync(
                user.Email,
                user.FullName,
                rawToken,
                expiresAt,
                cancellationToken);
        }
        catch (Exception exception)
        {
            // A delivery failure must not reveal that the account exists.
            _logger.LogError(
                exception,
                "Failed to send the password reset email for user {UserId}.",
                user.Id);
        }

        return new ForgotPasswordResponseDto(GenericMessage);
    }
}
