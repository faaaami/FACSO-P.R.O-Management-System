using FluentValidation;
using MediatR;
using PROManagementSystem.Application.Auth.Common;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Auth.Commands.AcceptInvitation;

public sealed record AcceptInvitationCommand(
    string Token,
    string NewPassword
) : IRequest<AcceptInvitationResponseDto>;

/// <param name="CanSignIn">
/// False while the account still awaits Admin approval.
/// </param>
public sealed record AcceptInvitationResponseDto(
    string Email,
    bool CanSignIn,
    string Message
);

public sealed class AcceptInvitationCommandValidator
    : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage("Invitation token is required.")
            .MaximumLength(256);

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("Password is required.")
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters long.")
            .MaximumLength(100);
    }
}

/// <summary>
/// An invited client contact sets their own password through the emailed
/// link. The link works once and expires (Auth:Tokens:InvitationLifetimeHours).
/// </summary>
public sealed class AcceptInvitationCommandHandler
    : IRequestHandler<AcceptInvitationCommand, AcceptInvitationResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public AcceptInvitationCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<AcceptInvitationResponseDto> Handle(
        AcceptInvitationCommand request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        await using var transaction =
            await _context.BeginTransactionAsync(cancellationToken);

        var user = await PasswordTokenRedemption.RedeemAsync(
            _context,
            request.Token,
            AccountTokenPurpose.Invitation,
            now,
            cancellationToken);

        PasswordTokenRedemption.SetPassword(
            user,
            _passwordHasher.Hash(request.NewPassword),
            now);

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = "InvitationAccepted",
            EntityType = "User",
            EntityId = user.Id,
            Description = "Invited user set their password.",
            CreatedAt = now,
            UpdatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AcceptInvitationResponseDto(
            user.Email,
            user.IsApproved,
            user.IsApproved
                ? "Your password has been set. You can now sign in."
                : "Your password has been set. You can sign in once FASCO approves your account.");
    }
}
