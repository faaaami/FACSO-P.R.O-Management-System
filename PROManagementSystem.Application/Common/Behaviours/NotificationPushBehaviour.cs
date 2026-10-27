using MediatR;
using PROManagementSystem.Application.Common.Realtime;

namespace PROManagementSystem.Application.Common.Behaviours;

/// <summary>
/// Sends the realtime pushes queued during a request once the request has
/// completed successfully, i.e. after its handler committed.
/// </summary>
/// <remarks>
/// <see cref="Services.UserNotifier"/> saves notification rows inside the
/// caller's unit of work and only enqueues the push, so a client is never told
/// about a notification that a rolled-back transaction then discards. When the
/// handler throws, nothing is flushed and the scoped queue is dropped with the
/// request. Handlers that already flush the queue themselves leave it empty,
/// which makes this a no-op for them.
/// </remarks>
public sealed class NotificationPushBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IPendingNotificationPushQueue _pendingPushes;

    public NotificationPushBehaviour(
        IPendingNotificationPushQueue pendingPushes)
    {
        _pendingPushes = pendingPushes;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next();

        await _pendingPushes.FlushAsync(cancellationToken);

        return response;
    }
}
