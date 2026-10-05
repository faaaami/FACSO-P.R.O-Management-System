using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Tasks.Commands.SetRenewalTaskServiceFee;

public sealed record SetRenewalTaskServiceFeeResponseDto(
    Guid TaskId,
    decimal ServiceFeeAmount,
    bool IsChange,
    RenewalTaskStatus Status,
    DateTime UpdatedAt
);
