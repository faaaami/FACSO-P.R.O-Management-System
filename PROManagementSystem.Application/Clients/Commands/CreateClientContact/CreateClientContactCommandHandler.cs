using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Clients.Common;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Models;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Clients.Commands.CreateClientContact;

/// <summary>
/// Creates a client contact (by an Admin, or an Agent for an assigned client)
/// and emails them an invitation link to set their own password. The account
/// cannot sign in until the invitation is accepted and an Admin approves it.
/// </summary>
public sealed class CreateClientContactCommandHandler
    : IRequestHandler<
        CreateClientContactCommand,
        CreateClientContactResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly ICurrentUserService _currentUser;
    private readonly AccountTokenOptions _tokenOptions;
    private readonly ILogger<CreateClientContactCommandHandler> _logger;

    public CreateClientContactCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        ICurrentUserService currentUser,
        IOptions<AccountTokenOptions> tokenOptions,
        ILogger<CreateClientContactCommandHandler> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _currentUser = currentUser;
        _tokenOptions = tokenOptions.Value;
        _logger = logger;
    }

    public async Task<CreateClientContactResponseDto> Handle(
        CreateClientContactCommand request,
        CancellationToken cancellationToken)
    {
        var clientCompany =
            await ClientContactAccess.GetInvitableCompanyAsync(
                _context,
                _currentUser,
                request.ClientCompanyId,
                cancellationToken);

        // Normalize once and use the same value for the duplicate check and
        // the stored column. Soft-deleted users are excluded by the global
        // query filter, so a released email can be reused.
        var normalizedEmail =
            request.Email.Trim().ToLowerInvariant();

        var emailExists =
            await _context.Users
                .AnyAsync(
                    x => x.Email.ToLower() == normalizedEmail,
                    cancellationToken);

        if (emailExists)
        {
            throw new InvalidOperationException(
                "A user with this email already exists.");
        }

        var now = DateTime.UtcNow;

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            Phone = request.Phone?.Trim(),
            // Nobody knows this password; the contact sets theirs from the
            // invitation link.
            PasswordHash = _passwordHasher.Hash(
                AccountTokens.UnusablePassword()),
            Role = UserRole.Client,
            ClientCompanyId = request.ClientCompanyId,
            IsActive = true,
            IsApproved = false,
            // Verified when the invitation link is used.
            EmailVerified = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Users.Add(user);

        var (rawToken, expiresAt) = ClientInvitations.Issue(
            _context,
            user,
            _tokenOptions,
            now);

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ClientContactCreated",
            EntityType = "User",
            EntityId = user.Id,
            Description =
                $"Client contact '{user.FullName}' was invited for company {request.ClientCompanyId}.",
            CreatedAt = now,
            UpdatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);

        var invitationSent = await ClientInvitations.TrySendAsync(
            _emailService,
            _logger,
            user,
            clientCompany.CompanyName,
            rawToken,
            expiresAt,
            cancellationToken);

        return new CreateClientContactResponseDto(
            user.Id,
            user.ClientCompanyId!.Value,
            user.FullName,
            user.Email,
            user.Phone,
            user.Role.ToString(),
            user.IsActive,
            user.IsApproved,
            invitationSent,
            expiresAt);
    }
}
