using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Realtime;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Services;

public sealed class UserNotifier : IUserNotifier
{
    private readonly IApplicationDbContext _context;
    private readonly IRealtimeNotificationService _realtime;
    private readonly ILogger<UserNotifier> _logger;

    public UserNotifier(
        IApplicationDbContext context,
        IRealtimeNotificationService realtime,
        ILogger<UserNotifier> logger)
    {
        _context = context;
        _realtime = realtime;
        _logger = logger;
    }

    public async Task NotifyAdminsAsync(
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        var adminIds = await _context.Users
            .Where(x => x.Role == UserRole.Admin && x.IsActive && !x.IsDeleted)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        await NotifyUsersAsync(adminIds, title, message, cancellationToken);
    }

    public async Task NotifyClientContactsAsync(
        Guid clientCompanyId,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        var contactIds = await _context.Users
            .Where(x => x.ClientCompanyId == clientCompanyId &&
                        x.Role == UserRole.Client &&
                        x.IsActive && x.IsApproved && !x.IsDeleted)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        await NotifyUsersAsync(contactIds, title, message, cancellationToken);
    }

    public async Task NotifyUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var created = userIds.Distinct().Select(userId => new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Message = message,
            CreatedAt = now,
            UpdatedAt = now
        }).ToList();

        _context.Notifications.AddRange(created);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var notification in created)
        {
            try
            {
                await _realtime.SendNotificationReceivedAsync(
                    notification.UserId,
                    new NotificationReceivedDto(
                        notification.Id,
                        notification.Title,
                        notification.Message,
                        notification.IsRead,
                        notification.CreatedAt),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // The notification row is saved; a failed push only delays it
                // until the next refresh, so it must not fail the request.
                _logger.LogWarning(ex, "Realtime push failed for notification {NotificationId}.", notification.Id);
            }
        }
    }
}
