using MediatR;
using PROManagementSystem.Application.Clients.Queries.GetClientEntityById;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Client.Queries.GetMyEntityById;

public sealed class GetMyEntityByIdQueryHandler
    : IRequestHandler<GetMyEntityByIdQuery, GetClientEntityByIdResponseDto?>
{
    private readonly IClientEntityReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetMyEntityByIdQueryHandler(
        IClientEntityReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetClientEntityByIdResponseDto?> Handle(
        GetMyEntityByIdQuery request,
        CancellationToken cancellationToken)
    {
        var clientCompanyId = _currentUser.ClientCompanyId;

        if (!clientCompanyId.HasValue)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        return await _repository.GetByIdAsync(
            clientCompanyId.Value,
            request.EntityId,
            includeDeleted: false,
            cancellationToken);
    }
}