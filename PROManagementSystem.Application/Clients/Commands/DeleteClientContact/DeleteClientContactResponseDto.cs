namespace PROManagementSystem.Application.Clients.Commands.DeleteClientContact;

public sealed record DeleteClientContactResponseDto(
    Guid Id,
    bool IsDeleted,
    bool IsActive,
    DateTime DeletedAt
);