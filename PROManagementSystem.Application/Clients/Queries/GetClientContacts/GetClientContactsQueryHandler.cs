using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Clients.Queries.GetClientContacts;

public sealed class GetClientContactsQueryHandler
    : IRequestHandler<
        GetClientContactsQuery,
        GetClientContactsResponseDto>
{
    private readonly IClientContactReadRepository _repository;

    public GetClientContactsQueryHandler(
        IClientContactReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetClientContactsResponseDto> Handle(
        GetClientContactsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Page < 1)
        {
            throw new ArgumentException(
                "Page must be greater than or equal to 1.");
        }

        if (request.PageSize < 1 || request.PageSize > 100)
        {
            throw new ArgumentException(
                "PageSize must be between 1 and 100.");
        }

        return await _repository.GetPagedAsync(
            request.ClientCompanyId,
            request.Page,
            request.PageSize,
            request.Search,
            request.IncludeDeleted,
            cancellationToken);
    }
}