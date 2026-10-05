using MediatR;
using Microsoft.Extensions.Logging;
using PROManagementSystem.Application.Common.DomainEvents;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Realtime;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Notifications.EventHandlers;

public sealed class ClientNotificationOnServiceRequestSubmittedHandler
    : INotificationHandler<ServiceRequestSubmittedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRealtimeNotificationService _realtime;
    private readonly ILogger<
        ClientNotificationOnServiceRequestSubmittedHandler> _logger;

    public ClientNotificationOnServiceRequestSubmittedHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IRealtimeNotificationService realtime,
        ILogger<ClientNotificationOnServiceRequestSubmittedHandler>
            logger)
    {
        _context = context;
        _currentUser = currentUser;
        _realtime = realtime;
        _logger = logger;
    }

    public async Task Handle(
        ServiceRequestSubmittedEvent notification,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            _logger.LogWarning(
                "Skipping client notification for service request {ServiceRequestId}: no authenticated user.",
                notification.ServiceRequestId);

            return;
        }

        var userId = _currentUser.UserId.Value;
        var now = DateTime.UtcNow;

        var clientNotification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Service request submitted",
            Message =
                $"ServiceRequestSubmitted | ServiceRequestId:{notification.ServiceRequestId} | " +
                $"Type: {notification.Type}.",
            IsRead = false,
            ReadAt = null,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };

        _context.Notifications.Add(clientNotification);

        await _context.SaveChangesAsync(
            cancellationToken);

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
                userId,
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