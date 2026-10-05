using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PROManagementSystem.Application.Common.DomainEvents;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Realtime;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Notifications.EventHandlers;

public sealed class ClientNotificationOnRenewalTaskCompletedHandler
    : INotificationHandler<RenewalTaskCompletedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IPendingNotificationPushQueue _pendingPushes;
    private readonly ILogger<
        ClientNotificationOnRenewalTaskCompletedHandler> _logger;

    public ClientNotificationOnRenewalTaskCompletedHandler(
        IApplicationDbContext context,
        IPendingNotificationPushQueue pendingPushes,
        ILogger<ClientNotificationOnRenewalTaskCompletedHandler> logger)
    {
        _context = context;
        _pendingPushes = pendingPushes;
        _logger = logger;
    }

    public async Task Handle(
        RenewalTaskCompletedEvent notification,
        CancellationToken cancellationToken)
    {
        var clientUsers =
            await _context.Users
                .Where(x =>
                    x.Role == UserRole.Client &&
                    x.ClientCompanyId ==
                        notification.ClientCompanyId &&
                    x.IsActive &&
                    !x.IsDeleted)
                .ToListAsync(cancellationToken);

        if (clientUsers.Count == 0)
        {
            _logger.LogInformation(
                "No active client users found for company {ClientCompanyId} when completing task {RenewalTaskId}.",
                notification.ClientCompanyId,
                notification.RenewalTaskId);

            return;
        }

        var now = DateTime.UtcNow;

        var createdNotifications =
            new List<Notification>(clientUsers.Count);

        foreach (var clientUser in clientUsers)
        {
            var clientNotification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = clientUser.Id,
                Title = "Renewal task completed",
                Message =
                    $"RenewalTaskCompleted | TaskId:{notification.RenewalTaskId}.",
                IsRead = false,
                ReadAt = null,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            };

            _context.Notifications.Add(clientNotification);
            createdNotifications.Add(clientNotification);
        }

        await _context.SaveChangesAsync(
            cancellationToken);

        // P10.6-B: the push here is deferred, not sent. This handler runs while
        // the task-completion transaction is still open, so pushing now could
        // announce a completion that later rolls back and would hold the task row
        // lock across network I/O. The command handler that owns the transaction
        // flushes the queue after it commits.
        foreach (var clientNotification in createdNotifications)
        {
            var payload =
                new NotificationReceivedDto(
                    clientNotification.Id,
                    clientNotification.Title,
                    clientNotification.Message,
                    clientNotification.IsRead,
                    clientNotification.CreatedAt);

            _pendingPushes.Enqueue(
                clientNotification.UserId,
                payload);
        }
    }
}