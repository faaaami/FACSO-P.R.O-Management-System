using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Common.Realtime;

/// <summary>
/// Default scoped implementation of <see cref="IPendingNotificationPushQueue"/>.
///
/// A single instance is created per request scope, shared by the command handler
/// that owns the transaction and the completion handlers that publish into it, so
/// everything they enqueue is flushed once, by the owner, after the commit.
/// </summary>
public sealed class PendingNotificationPushQueue
    : IPendingNotificationPushQueue
{
    private readonly ConcurrentQueue<PendingPush> _pending = new();
    private readonly IRealtimeNotificationService _realtime;
    private readonly ILogger<PendingNotificationPushQueue> _logger;

    public PendingNotificationPushQueue(
        IRealtimeNotificationService realtime,
        ILogger<PendingNotificationPushQueue> logger)
    {
        _realtime = realtime;
        _logger = logger;
    }

    public void Enqueue(
        Guid userId,
        NotificationReceivedDto payload)
        => _pending.Enqueue(
            new PendingPush(userId, payload));

    public async Task FlushAsync(
        CancellationToken cancellationToken)
    {
        while (_pending.TryDequeue(out var push))
        {
            try
            {
                await _realtime.SendNotificationReceivedAsync(
                    push.UserId,
                    push.Payload,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // Deliberately swallowed, exactly as the in-transaction push was
                // before this queue existed: the completion is already committed,
                // so a delivery failure must not be reported as a failed
                // operation. The notification row remains in the client's inbox.
                _logger.LogError(
                    ex,
                    "Failed to push deferred realtime notification for user {UserId}.",
                    push.UserId);
            }
        }
    }

    private readonly record struct PendingPush(
        Guid UserId,
        NotificationReceivedDto Payload);
}
