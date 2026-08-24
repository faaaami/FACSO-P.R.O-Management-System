using PROManagementSystem.Application.Notifications.Queries.GetNotifications;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface INotificationReadRepository
{
    Task<(IReadOnlyList<NotificationDto> Items, int TotalCount)>
        GetByUserIdAsync(
            Guid userId,
            int page,
            int pageSize,
            string sortBy,
            string sortDir,
            CancellationToken cancellationToken = default);
}