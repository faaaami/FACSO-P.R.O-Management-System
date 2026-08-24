using MediatR;

namespace PROManagementSystem.Application.Clients.Queries.GetClientContacts;

public sealed record GetClientContactsQuery(
    Guid ClientCompanyId,
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool IncludeDeleted = false
) : IRequest<GetClientContactsResponseDto>;