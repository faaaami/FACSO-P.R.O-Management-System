using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Clients.Queries.GetClientEntityById;

public sealed class GetClientEntityByIdQueryHandler
    : IRequestHandler<
        GetClientEntityByIdQuery,
        GetClientEntityByIdResponseDto?>
{
    private readonly IClientEntityReadRepository _repository;

    public GetClientEntityByIdQueryHandler(
        IClientEntityReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetClientEntityByIdResponseDto?> Handle(
        GetClientEntityByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(
            request.ClientCompanyId,
            request.EntityId,
            request.IncludeDeleted,
            cancellationToken);
    }
}