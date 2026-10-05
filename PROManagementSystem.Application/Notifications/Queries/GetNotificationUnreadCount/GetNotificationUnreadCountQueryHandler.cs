using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Notifications.Queries.GetNotificationUnreadCount;

public sealed class GetNotificationUnreadCountQueryHandler
    : IRequestHandler<
        GetNotificationUnreadCountQuery,
        int>
{
    private readonly INotificationReadRepository _notificationReadRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetNotificationUnreadCountQueryHandler(
        INotificationReadRepository notificationReadRepository,
        ICurrentUserService currentUserService)
    {
        _notificationReadRepository = notificationReadRepository;
        _currentUserService = currentUserService;
    }

    public async Task<int> Handle(
        GetNotificationUnreadCountQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated ||
            !_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "User is not authenticated.");
        }

        var userId = _currentUserService.UserId.Value;

        return await _notificationReadRepository.GetUnreadCountAsync(
            userId,
            cancellationToken);
    }
}