using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Auth.Common;
using PROManagementSystem.Application.Clients.Common;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Models;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Clients.Commands.ResendClientInvitation;

public sealed record ResendClientInvitationCommand(
    Guid ClientCompanyId,
    Guid ContactId
) : IRequest<ResendClientInvitationResponseDto>;

public sealed record ResendClientInvitationResponseDto(
    Guid ContactId,
    bool InvitationSent,
    DateTime InvitationExpiresAt
);

/// <summary>
/// Issues a new invitation link for a contact who has not set a password yet.
/// Earlier invitation links stop working.
/// </summary>
public sealed class ResendClientInvitationCommandHandler
    : IRequestHandler<
        ResendClientInvitationCommand,
        ResendClientInvitationResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ICurrentUserService _currentUser;
    private readonly AccountTokenOptions _tokenOptions;
    private readonly ILogger<ResendClientInvitationCommandHandler> _logger;

    public ResendClientInvitationCommandHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        ICurrentUserService currentUser,
        IOptions<AccountTokenOptions> tokenOptions,
        ILogger<ResendClientInvitationCommandHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _currentUser = currentUser;
        _tokenOptions = tokenOptions.Value;
        _logger = logger;
    }

    public async Task<ResendClientInvitationResponseDto> Handle(
        ResendClientInvitationCommand request,
        CancellationToken cancellationToken)
    {
        var company = await ClientContactAccess.GetInvitableCompanyAsync(
            _context,
            _currentUser,
            request.ClientCompanyId,
            cancellationToken);

        var contact = await _context.Users
            .FirstOrDefaultAsync(
                x => x.Id == request.ContactId &&
                     x.ClientCompanyId == request.ClientCompanyId &&
                     x.Role == UserRole.Client,
                cancellationToken);

        if (contact is null)
        {
            throw new KeyNotFoundException(
                "Client contact was not found.");
        }

        if (contact.EmailVerified)
        {
            throw new ConflictException(
                "This contact has already set a password. " +
                "They can use 'Forgot password' to choose a new one.");
        }

        var now = DateTime.UtcNow;

        await PasswordTokenRedemption.RevokeOutstandingAsync(
            _context,
            contact.Id,
            AccountTokenPurpose.Invitation,
            now,
            cancellationToken);

        var (rawToken, expiresAt) = ClientInvitations.Issue(
            _context,
            contact,
            _tokenOptions,
            now);

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ClientInvitationResent",
            EntityType = "User",
            EntityId = contact.Id,
            Description =
                $"Invitation was resent to client contact '{contact.FullName}'.",
            CreatedAt = now,
            UpdatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);

        var sent = await ClientInvitations.TrySendAsync(
            _emailService,
            _logger,
            contact,
            company.CompanyName,
            rawToken,
            expiresAt,
            cancellationToken);

        return new ResendClientInvitationResponseDto(
            contact.Id,
            sent,
            expiresAt);
    }
}
