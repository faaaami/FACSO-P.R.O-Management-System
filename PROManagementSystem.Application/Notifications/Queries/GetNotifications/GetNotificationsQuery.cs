using MediatR;

namespace PROManagementSystem.Application.Notifications.Queries.GetNotifications;

public sealed record GetNotificationsQuery(
    int Page = 1,
    int PageSize = 20,
    string? SortBy = "createdAt",
    string? SortDir = "desc"
) : IRequest<GetNotificationsResponseDto>;