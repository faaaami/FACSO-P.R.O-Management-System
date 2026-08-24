using MediatR;

namespace PROManagementSystem.Application.Notifications.Commands.MarkNotificationAsRead;

public sealed record MarkNotificationAsReadCommand(
    Guid NotificationId
) : IRequest<MarkNotificationAsReadResponseDto>;