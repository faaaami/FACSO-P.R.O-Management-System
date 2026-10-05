using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Auth.Commands.Login;

public sealed class LoginCommandHandler
    : IRequestHandler<LoginCommand, LoginResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ILogger<LoginCommandHandler> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<LoginResponseDto> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        // The submitted address is normalized before the lookup because every
        // write path already stores a normalized value: CreateStaff and Register
        // both write Trim().ToLowerInvariant(), and ForgotPassword normalizes the
        // same way before it searches. Comparing the raw request against the
        // stored column therefore made a valid login fail with
        // "Invalid email or password" whenever the user typed the address in a
        // different case or with stray surrounding whitespace — a correct
        // password rejected for a formatting difference that no other entry
        // point in the system treats as meaningful.
        var normalizedEmail =
            request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(
                x => x.Email.ToLower() == normalizedEmail,
                cancellationToken);

        if (user is null)
        {
            await TryRecordFailedLoginAsync(
                Guid.Empty,
                "Login failed: unknown email.",
                cancellationToken);

            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new ForbiddenAccessException(
                "Your account is inactive.");
        }

        var passwordValid = _passwordHasher.Verify(
            request.Password,
            user.PasswordHash);

        if (!passwordValid)
        {
            await TryRecordFailedLoginAsync(
                user.Id,
                "Login failed: incorrect password.",
                cancellationToken);

                throw new UnauthorizedAccessException(
                "Invalid email or password.");
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

        var accessToken = _tokenService.GenerateAccessToken(user);

        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken =
            _tokenService.HashRefreshToken(refreshToken);


        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

        user.UpdatedAt = DateTime.UtcNow;

        var loginAudit = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = "LoginSucceeded",
            EntityType = "User",
            EntityId = user.Id,
            Description =
                "User logged in successfully.",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(loginAudit);

        await _context.SaveChangesAsync(cancellationToken);

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 60 * 60,
            User = new LoginUserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role.ToString(),
                ClientCompanyId = user.ClientCompanyId
            }
        };
    }

    private async Task TryRecordFailedLoginAsync(
        Guid entityId,
        string description,
        CancellationToken cancellationToken)
    {
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = null,
            Action = "LoginFailed",
            EntityType = "User",
            EntityId = entityId,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        try
        {
            await _context.SaveChangesAsync(
                cancellationToken);
        }
        catch (Exception ex)
        {
            // A best-effort audit must never break the login flow.
            _logger.LogError(
                ex,
                "Failed to write login audit log.");
        }
    }
}