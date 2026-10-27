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
/// Tells active admins that a client company has raised a service request
/// awaiting a decision, since only admins can convert or reject it and the
/// client has no other way of learning that it is in the queue.
/// </summary>
public sealed class AdminNotificationOnServiceRequestSubmittedHandler
    : INotificationHandler<ServiceRequestSubmittedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IRealtimeNotificationService _realtime;
    private readonly ILogger<
        AdminNotificationOnServiceRequestSubmittedHandler> _logger;

    public AdminNotificationOnServiceRequestSubmittedHandler(
        IApplicationDbContext context,
        IRealtimeNotificationService realtime,
        ILogger<AdminNotificationOnServiceRequestSubmittedHandler>
            logger)
    {
        _context = context;
        _realtime = realtime;
        _logger = logger;
    }

    public async Task Handle(
        ServiceRequestSubmittedEvent notification,
        CancellationToken cancellationToken)
    {
        var adminUsers =
            await _context.Users
                .Where(x =>
                    x.Role == UserRole.Admin &&
                    x.IsActive &&
                    !x.IsDeleted)
                .ToListAsync(cancellationToken);

        if (adminUsers.Count == 0)
        {
            _logger.LogInformation(
                "No active admin users found when service request {ServiceRequestId} was submitted.",
                notification.ServiceRequestId);

            return;
        }

        var now = DateTime.UtcNow;

        var details = await ServiceRequestNotificationText.DescribeAsync(
            _context, notification, cancellationToken);

        var createdNotifications =
            new List<Notification>(adminUsers.Count);

        foreach (var adminUser in adminUsers)
        {
            var adminNotification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = adminUser.Id,
                Title = "New service request",
                Message =
                    $"{details.CompanyName} requested {details.Subject}. " +
                    "Review it in Service requests.",
                IsRead = false,
                ReadAt = null,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                DeletedAt = null
            };

            _context.Notifications.Add(adminNotification);
            createdNotifications.Add(adminNotification);
        }

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var adminNotification in createdNotifications)
        {
            var payload =
                new NotificationReceivedDto(
                    adminNotification.Id,
                    adminNotification.Title,
                    adminNotification.Message,
                    adminNotification.IsRead,
                    adminNotification.CreatedAt);

            try
            {
                await _realtime.SendNotificationReceivedAsync(
                    adminNotification.UserId,
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
