namespace PROManagementSystem.Application.Notifications.Commands.MarkNotificationAsRead;

public sealed record MarkNotificationAsReadResponseDto(
    Guid Id,
    bool IsRead,
    DateTime? ReadAt
);