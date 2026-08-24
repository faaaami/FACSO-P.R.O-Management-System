using MediatR;

namespace PROManagementSystem.Application.Clients.Queries.GetClientCompanyById;

public sealed record GetClientCompanyByIdQuery(
    Guid Id,
    bool IncludeDeleted = false
) : IRequest<GetClientCompanyByIdResponseDto?>;