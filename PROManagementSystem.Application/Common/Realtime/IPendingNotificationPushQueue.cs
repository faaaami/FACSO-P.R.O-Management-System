namespace PROManagementSystem.Application.Common.Realtime;

/// <summary>
/// Defers realtime notification pushes until the caller's unit of work has
/// committed.
///
/// P10.6-B: completion handlers run inside the task-completion transaction (the
/// publish happens before the commit, so the notification rows and the service
/// fee invoice commit atomically). Pushing to SignalR from inside that window
/// meant a client could be told a renewal task was completed and then have the
/// completion roll back, and it held the task row lock across network I/O.
///
/// A completion handler therefore enqueues the push here, and the command handler
/// that owns the transaction flushes the queue only after <c>CommitAsync</c>
/// returns. The queue is scoped: it lives for one request/transaction and is
/// empty at the start of the next.
/// </summary>
public interface IPendingNotificationPushQueue
{
    /// <summary>
    /// Queues one notification for delivery to <paramref name="userId"/>.
    /// Ordering is preserved: pushes flush in the order they were enqueued.
    /// </summary>
    void Enqueue(Guid userId, NotificationReceivedDto payload);

    /// <summary>
    /// Delivers every queued notification, in order, and empties the queue.
    /// Must be called only after the owning transaction has committed: a push
    /// that fails is logged and dropped so it can never fail an operation whose
    /// database state is already durable.
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken);
}
