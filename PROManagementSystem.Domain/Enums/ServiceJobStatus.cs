namespace PROManagementSystem.Domain.Enums;

public enum ServiceJobStatus
{
    New = 1,
    InProgress = 2,

    /// <summary>Waiting on the client or the authority; a reason is recorded.</summary>
    OnHold = 3,

    Completed = 4,
    Cancelled = 5
}
