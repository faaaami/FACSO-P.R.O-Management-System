namespace PROManagementSystem.Application.Tasks.Commands.AddRenewalStep;

public sealed record AddRenewalStepRequest(
    string StepName,
    string? ReferenceNumber,
    string? ProofFileUrl
);