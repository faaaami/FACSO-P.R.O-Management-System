using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PROManagementSystem.Application.Common.DomainEvents;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Realtime;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Notifications.EventHandlers;

/// <summary>
/// Tells the client company the outcome of its service request: either that an
/// admin converted it into a renewal task, or why it was rejected.
/// </summary>
public sealed class ClientNotificationOnServiceRequestDecidedHandler
    : INotificationHandler<ServiceRequestDecidedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IRealtimeNotificationService _realtime;
    private readonly ILogger<
        ClientNotificationOnServiceRequestDecidedHandler> _logger;

    public ClientNotificationOnServiceRequestDecidedHandler(
        IApplicationDbContext context,
        IRealtimeNotificationService realtime,
        ILogger<ClientNotificationOnServiceRequestDecidedHandler>
            logger)
    {
        _context = context;
        _realtime = realtime;
        _logger = logger;
    }

    public async Task Handle(
        ServiceRequestDecidedEvent notification,
        CancellationToken cancellationToken)
    {
        var isConverted =
            notification.Status == ServiceRequestStatus.Converted;

        var title = isConverted
            ? "Service request converted"
            : "Service request rejected";

        // The rejection reason is included because it is the only place the
        // client can learn why its request was turned down.
        var message = isConverted
            ? $"ServiceRequestConverted | ServiceRequestId:{notification.ServiceRequestId} | " +
              $"RenewalTaskId:{notification.ConvertedRenewalTaskId}."
            : $"ServiceRequestRejected | ServiceRequestId:{notification.ServiceRequestId} | " +
              $"Reason: {notification.RejectionReason}";

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
                "No active client users found for company {ClientCompanyId} when service request {ServiceRequestId} was decided.",
                notification.ClientCompanyId,
                notification.ServiceRequestId);

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
                Title = title,
                Message = message,
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

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var clientNotification in createdNotifications)
        {
            var payload =
                new NotificationReceivedDto(
                    clientNotification.Id,
                    clientNotification.Title,
                    clientNotification.Message,
                    clientNotification.IsRead,
                    clientNotification.CreatedAt);

            try
            {
                await _realtime.SendNotificationReceivedAsync(
                    clientNotification.UserId,
                    payload,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to push realtime notification for service request {ServiceRequestId}.",
                    notification.ServiceRequestId);
            }
        }
    }
}
