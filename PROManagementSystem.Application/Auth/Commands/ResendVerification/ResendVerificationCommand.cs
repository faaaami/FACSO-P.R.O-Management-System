using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Models;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Auth.Commands.ResendVerification;

public sealed record ResendVerificationCommand(
    string Email
) : IRequest<ResendVerificationResponseDto>;

public sealed record ResendVerificationResponseDto(
    string Message
);

public sealed class ResendVerificationCommandValidator
    : AbstractValidator<ResendVerificationCommand>
{
    public ResendVerificationCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);
    }
}

/// <summary>
/// Sends a fresh verification link to an active, unverified account. The
/// response is the same whether or not a link was sent.
/// </summary>
public sealed class ResendVerificationCommandHandler
    : IRequestHandler<ResendVerificationCommand, ResendVerificationResponseDto>
{
    public const string GenericMessage =
        "If this account needs verification, a new link has been sent.";

    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly AccountTokenOptions _tokenOptions;
    private readonly SystemAccountOptions _systemAccounts;
    private readonly ILogger<ResendVerificationCommandHandler> _logger;

    public ResendVerificationCommandHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        IOptions<AccountTokenOptions> tokenOptions,
        IOptions<SystemAccountOptions> systemAccounts,
        ILogger<ResendVerificationCommandHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _tokenOptions = tokenOptions.Value;
        _systemAccounts = systemAccounts.Value;
        _logger = logger;
    }

    public async Task<ResendVerificationResponseDto> Handle(
        ResendVerificationCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(
                x => x.Email == normalizedEmail &&
                     x.IsActive &&
                     !x.EmailVerified &&
                     x.Id != _systemAccounts.AutomationUserId,
                cancellationToken);

        if (user is null)
        {
            return new ResendVerificationResponseDto(GenericMessage);
        }

        var now = DateTime.UtcNow;

        await _context.EmailVerificationTokens
            .Where(x => x.UserId == user.Id && x.UsedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.UsedAt, now)
                    .SetProperty(x => x.UpdatedAt, now),
                cancellationToken);

        var rawToken = AccountTokens.Generate();
        var expiresAt = now.AddHours(
            Math.Max(1, _tokenOptions.EmailVerificationLifetimeHours));

        _context.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = AccountTokens.Hash(rawToken),
            ExpiresAt = expiresAt,
            CreatedAt = now,
            UpdatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await _emailService.SendEmailVerificationAsync(
                user.Email,
                user.FullName,
                rawToken,
                expiresAt,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to send the verification email for user {UserId}.",
                user.Id);
        }

        return new ResendVerificationResponseDto(GenericMessage);
    }
}
