using MediatR;
using PROManagementSystem.Application.Auth.Common;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

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
        var now = DateTime.UtcNow;

        await using var transaction =
            await _context.BeginTransactionAsync(cancellationToken);

        var user = await PasswordTokenRedemption.RedeemAsync(
            _context,
            request.Token,
            AccountTokenPurpose.PasswordReset,
            now,
            cancellationToken);

        PasswordTokenRedemption.SetPassword(
            user,
            _passwordHasher.Hash(request.NewPassword),
            now);

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = null,
            Action = "PasswordReset",
            EntityType = "User",
            EntityId = user.Id,
            Description = "User password was reset.",
            CreatedAt = now,
            UpdatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ResetPasswordResponseDto(
            "Password has been reset successfully.");
    }
}
