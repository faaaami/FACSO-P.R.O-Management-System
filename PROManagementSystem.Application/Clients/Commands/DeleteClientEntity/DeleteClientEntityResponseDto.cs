namespace PROManagementSystem.Application.Clients.Commands.DeleteClientEntity;

public sealed record DeleteClientEntityResponseDto(
    Guid Id,
    bool IsDeleted,
    DateTime DeletedAt
);