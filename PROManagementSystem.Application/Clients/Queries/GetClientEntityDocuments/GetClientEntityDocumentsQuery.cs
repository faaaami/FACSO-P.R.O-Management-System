using MediatR;

namespace PROManagementSystem.Application.Clients.Queries.GetClientEntityDocuments;

public sealed record GetClientEntityDocumentsQuery(
    Guid ClientEntityId,
    bool IncludeDeleted = false
) : IRequest<GetClientEntityDocumentsResponseDto>;