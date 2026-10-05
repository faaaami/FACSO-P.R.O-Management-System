using MediatR;

namespace PROManagementSystem.Application.Notifications.Queries.GetNotificationUnreadCount;

public sealed record GetNotificationUnreadCountQuery
    : IRequest<int>;