using MediatR;
using PROManagementSystem.Application.Clients.Queries.GetClientContacts;

namespace PROManagementSystem.Application.Client.Queries.GetMyContacts;

public sealed record GetMyContactsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null
) : IRequest<GetClientContactsResponseDto>;