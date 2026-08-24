using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Clients.Queries.GetClientCompanyById;

public sealed class GetClientCompanyByIdQueryHandler
    : IRequestHandler<
        GetClientCompanyByIdQuery,
        GetClientCompanyByIdResponseDto?>
{
    private readonly IClientCompanyReadRepository _repository;

    public GetClientCompanyByIdQueryHandler(
        IClientCompanyReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetClientCompanyByIdResponseDto?> Handle(
        GetClientCompanyByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(
            request.Id,
            request.IncludeDeleted,
            cancellationToken);
    }
}