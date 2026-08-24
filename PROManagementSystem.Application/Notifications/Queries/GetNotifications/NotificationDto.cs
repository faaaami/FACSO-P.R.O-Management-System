namespace PROManagementSystem.Application.Notifications.Queries.GetNotifications;

public sealed record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt
);