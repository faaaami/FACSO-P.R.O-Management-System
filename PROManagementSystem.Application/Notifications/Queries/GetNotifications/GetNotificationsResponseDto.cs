namespace PROManagementSystem.Application.Notifications.Queries.GetNotifications;

public sealed record GetNotificationsResponseDto(
    IReadOnlyList<NotificationDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);