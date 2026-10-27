using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Realtime;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Services;

/// <summary>
/// Saves notification rows in the caller's unit of work and queues the
/// realtime push. The push is sent by <c>NotificationPushBehaviour</c> after
/// the request completes, so it can never announce a notification that a
/// rolled-back transaction discards.
/// </summary>
public sealed class UserNotifier : IUserNotifier
{
    private readonly IApplicationDbContext _context;
    private readonly IPendingNotificationPushQueue _pendingPushes;

    public UserNotifier(
        IApplicationDbContext context,
        IPendingNotificationPushQueue pendingPushes)
    {
        _context = context;
        _pendingPushes = pendingPushes;
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
            _pendingPushes.Enqueue(
                notification.UserId,
                new NotificationReceivedDto(
                    notification.Id,
                    notification.Title,
                    notification.Message,
                    notification.IsRead,
                    notification.CreatedAt));
        }
    }
}
