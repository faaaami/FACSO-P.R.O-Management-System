using MediatR;
using PROManagementSystem.Application.Clients.Queries.GetClientCompanyById;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Client.Queries.GetMyCompany;

public sealed class GetMyCompanyQueryHandler
    : IRequestHandler<GetMyCompanyQuery, GetClientCompanyByIdResponseDto?>
{
    private readonly IClientCompanyReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetMyCompanyQueryHandler(
        IClientCompanyReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetClientCompanyByIdResponseDto?> Handle(
        GetMyCompanyQuery request,
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
            includeDeleted: false,
            cancellationToken);
    }
}