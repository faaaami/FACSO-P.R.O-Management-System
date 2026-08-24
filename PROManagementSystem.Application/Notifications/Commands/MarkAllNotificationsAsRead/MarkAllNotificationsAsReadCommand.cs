using MediatR;

namespace PROManagementSystem.Application.Notifications.Commands.MarkAllNotificationsAsRead;

public sealed record MarkAllNotificationsAsReadCommand
    : IRequest<MarkAllNotificationsAsReadResponseDto>;