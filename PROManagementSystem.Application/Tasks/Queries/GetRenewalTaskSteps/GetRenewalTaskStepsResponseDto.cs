namespace PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskSteps;

public sealed record GetRenewalTaskStepsResponseDto(
    Guid TaskId,
    IReadOnlyList<RenewalStepLogDto> Items,
    int TotalCount
);

public sealed record RenewalStepLogDto(
    Guid Id,
    string StepName,
    Guid CompletedBy,
    DateTime CompletedAt,
    string? ReferenceNumber,
    string? ProofFileUrl
);