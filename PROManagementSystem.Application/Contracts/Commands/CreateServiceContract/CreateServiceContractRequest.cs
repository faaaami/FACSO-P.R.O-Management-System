namespace PROManagementSystem.Application.Contracts.Commands.CreateServiceContract;

public sealed record CreateServiceContractRequest(
    string ContractNumber,
    DateTime StartDate,
    decimal RetainerAmount,
    string? Terms
);