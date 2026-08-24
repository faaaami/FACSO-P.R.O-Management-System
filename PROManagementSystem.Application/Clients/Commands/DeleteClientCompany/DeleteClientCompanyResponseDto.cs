namespace PROManagementSystem.Application.Clients.Commands.DeleteClientCompany;

public sealed record DeleteClientCompanyResponseDto(
    Guid Id,
    bool IsDeleted,
    DateTime DeletedAt
);