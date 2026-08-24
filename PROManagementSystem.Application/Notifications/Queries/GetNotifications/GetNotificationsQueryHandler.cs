using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Notifications.Queries.GetNotifications;

public sealed class GetNotificationsQueryHandler
    : IRequestHandler<
        GetNotificationsQuery,
        GetNotificationsResponseDto>
{
    private readonly INotificationReadRepository _notificationReadRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetNotificationsQueryHandler(
        INotificationReadRepository notificationReadRepository,
        ICurrentUserService currentUserService)
    {
        _notificationReadRepository = notificationReadRepository;
        _currentUserService = currentUserService;
    }

    public async Task<GetNotificationsResponseDto> Handle(
        GetNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated ||
            !_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "User is not authenticated.");
        }

        var userId = _currentUserService.UserId.Value;

        var result =
            await _notificationReadRepository.GetByUserIdAsync(
                userId,
                request.Page,
                request.PageSize,
                request.SortBy ?? "createdAt",
                request.SortDir ?? "desc",
                cancellationToken);

        return new GetNotificationsResponseDto(
            result.Items,
            request.Page,
            request.PageSize,
            result.TotalCount);
    }
}