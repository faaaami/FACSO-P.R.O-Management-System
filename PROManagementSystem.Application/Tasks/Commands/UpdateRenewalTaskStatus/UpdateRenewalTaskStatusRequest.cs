namespace PROManagementSystem.Api.Controllers.Tasks;

public sealed record UpdateRenewalTaskStatusRequest(
    string Status,
    string? Note
);
