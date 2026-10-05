using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Realtime;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Clients.Commands.ApproveClient;

public sealed class ApproveClientCommandHandler
    : IRequestHandler<ApproveClientCommand, ApproveClientResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRealtimeNotificationService _realtime;
    private readonly ILogger<ApproveClientCommandHandler> _logger;

    public ApproveClientCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IRealtimeNotificationService realtime,
        ILogger<ApproveClientCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _realtime = realtime;
        _logger = logger;
    }

    public async Task<ApproveClientResponseDto> Handle(
        ApproveClientCommand request,
        CancellationToken cancellationToken)
    {
        var contact = await _context.Users
            .FirstOrDefaultAsync(
                x =>
                    x.Id == request.ContactId &&
                    x.ClientCompanyId == request.ClientCompanyId &&
                    x.Role == UserRole.Client &&
                    !x.IsDeleted,
                cancellationToken);

        if (contact is null)
        {
            throw new KeyNotFoundException(
                "Client contact was not found.");
        }

        var utcNow = DateTime.UtcNow;

        if (contact.IsApproved)
        {
            return new ApproveClientResponseDto(
                contact.Id,
                contact.ClientCompanyId!.Value,
                contact.FullName,
                contact.Email,
                contact.Phone,
                contact.Role,
                contact.IsActive,
                contact.IsApproved,
                contact.EmailVerified,
                contact.UpdatedAt);
        }

        // P10.6-C: the approval, its audit row and the notification that tells
        // the contact about it now commit as ONE unit. They were previously two
        // independent SaveChanges with no transaction between them, so a failure
        // writing the notification left the account approved and audited while
        // the notification was lost for good — and the idempotent early-return
        // above answers a retry with success without ever producing it, so no
        // retry could recover the gap either.
        await using var transaction =
            await _context.BeginTransactionAsync(
                cancellationToken);

        // Guards the catch below. Once the commit lands the transaction is spent,
        // so a failure raised by the post-commit push must not be answered with a
        // rollback attempt against a completed transaction.
        var committed = false;

        try
        {
            contact.IsApproved = true;
            contact.IsActive = true;
            contact.UpdatedAt = utcNow;

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = _currentUser.UserId,
                Action = "ClientApproved",
                EntityType = "User",
                EntityId = contact.Id,
                Description =
                    $"Client account '{contact.FullName}' was approved.",
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
                IsDeleted = false,
                DeletedAt = null
            };
            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync(
                cancellationToken);

            // Enlists in the surrounding transaction: EF joins the caller's
            // transaction, so this save is no longer independently committed.
            var notification = await NotifyAsync(
                contact,
                "Account approved",
                "Your account has been approved by the administrator.",
                utcNow,
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            committed = true;

            // Realtime delivery is deliberately NOT part of the transaction. It
            // runs only after the commit succeeded, and a delivery failure is
            // logged rather than reported as a failed approval, matching the
            // existing behaviour this push already had.
            await PushAsync(
                contact,
                notification,
                "approved",
                cancellationToken);

            return new ApproveClientResponseDto(
                contact.Id,
                contact.ClientCompanyId!.Value,
                contact.FullName,
                contact.Email,
                contact.Phone,
                contact.Role,
                contact.IsActive,
                contact.IsApproved,
                contact.EmailVerified,
                contact.UpdatedAt);
        }
        catch
        {
            if (!committed)
            {
                await transaction.RollbackAsync(
                    cancellationToken);
            }

            throw;
        }
    }

    /// <summary>
    /// Persists the notification inside the caller's transaction and returns it.
    ///
    /// P10.6-C split delivery out of this method. It used to push to SignalR from
    /// here, i.e. before the surrounding transaction committed, so a contact could
    /// be told their account was approved and then have that approval roll back.
    /// This method now only writes; <see cref="PushAsync"/> delivers, after the
    /// commit.
    /// </summary>
    private async Task<Notification> NotifyAsync(
        User contact,
        string title,
        string message,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = contact.Id,
            Title = title,
            Message = message,
            IsRead = false,
            ReadAt = null,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            IsDeleted = false,
            DeletedAt = null
        };

        _context.Notifications.Add(notification);

        await _context.SaveChangesAsync(
            cancellationToken);

        return notification;
    }

    /// <summary>
    /// Delivers the already-committed notification to the contact.
    ///
    /// Failures are logged and swallowed: the approval is durable at this point, so
    /// a realtime delivery problem must not be reported as a failed approval. The
    /// row remains in the contact's inbox and is picked up on next load.
    /// </summary>
    private async Task PushAsync(
        User contact,
        Notification notification,
        string outcome,
        CancellationToken cancellationToken)
    {
        var payload = new NotificationReceivedDto(
            notification.Id,
            notification.Title,
            notification.Message,
            notification.IsRead,
            notification.CreatedAt);

        try
        {
            await _realtime.SendNotificationReceivedAsync(
                contact.Id,
                payload,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to push realtime notification for {Outcome} client {UserId}.",
                outcome,
                contact.Id);
        }
    }
}