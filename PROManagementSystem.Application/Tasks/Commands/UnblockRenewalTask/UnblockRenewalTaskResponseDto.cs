using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Commands.UnblockRenewalTask;

/// <summary>
/// The result of a successful unblock.
///
/// Carries the unblock reason back to the caller because it is the reason that
/// was just written to history and audit, and BlockRenewalTaskResponseDto echoes
/// its blocked reason on the same principle: the actor is told what was recorded
/// on their behalf, not merely that a write happened.
///
/// BlockedReason and BlockedSince are deliberately NOT echoed, because after a
/// successful unblock they are null on the task and this command is the only
/// thing that can have cleared them. Returning them would invite a caller to
/// render a value that no longer exists. They are gone from the CURRENT state and
/// preserved in history, which is the whole point of the operation.
/// </summary>
public sealed record UnblockRenewalTaskResponseDto(
    Guid TaskId,
    RenewalTaskStatus Status,
    string UnblockReason,
    DateTime UpdatedAt
);
