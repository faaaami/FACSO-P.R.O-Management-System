using MediatR;

namespace PROManagementSystem.Application.Tasks.Commands.AddRenewalStep;

public sealed record AddRenewalStepCommand(
    Guid TaskId,
    string StepName,
    string? ReferenceNumber,
    string? ProofFileUrl
) : IRequest<AddRenewalStepResponseDto>;