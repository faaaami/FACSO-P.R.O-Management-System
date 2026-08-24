namespace PROManagementSystem.Application.Tasks.Commands.AddRenewalStep;

public sealed record AddRenewalStepResponseDto(
    Guid Id,
    Guid RenewalTaskId,
    string StepName,
    Guid CompletedBy,
    DateTime CompletedAt,
    string? ReferenceNumber,
    string? ProofFileUrl
);