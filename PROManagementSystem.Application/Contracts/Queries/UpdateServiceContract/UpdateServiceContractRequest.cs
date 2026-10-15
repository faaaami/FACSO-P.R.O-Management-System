namespace PROManagementSystem.Application.Contracts.Commands.UpdateServiceContract;

public sealed record UpdateServiceContractRequest(
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal RetainerAmount,
    string? Terms
);