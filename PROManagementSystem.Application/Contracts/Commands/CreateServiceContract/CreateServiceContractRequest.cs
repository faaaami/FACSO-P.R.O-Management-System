namespace PROManagementSystem.Application.Contracts.Commands.CreateServiceContract;

public sealed record CreateServiceContractRequest(
    string ContractNumber,
    DateOnly StartDate,
    decimal RetainerAmount,
    string? Terms
);