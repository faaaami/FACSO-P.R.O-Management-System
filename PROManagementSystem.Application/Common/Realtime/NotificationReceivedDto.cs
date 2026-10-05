namespace PROManagementSystem.Application.Common.Realtime;

public sealed record NotificationReceivedDto(
    Guid Id,
    string Title,
    string Message,
    bool IsRead,
    DateTime CreatedAt
);