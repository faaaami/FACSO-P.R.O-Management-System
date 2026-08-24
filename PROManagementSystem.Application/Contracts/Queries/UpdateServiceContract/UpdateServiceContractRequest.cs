namespace PROManagementSystem.Application.Contracts.Commands.UpdateServiceContract;

public sealed record UpdateServiceContractRequest(
    DateTime StartDate,
    DateTime? EndDate,
    decimal RetainerAmount,
    string? Terms
);