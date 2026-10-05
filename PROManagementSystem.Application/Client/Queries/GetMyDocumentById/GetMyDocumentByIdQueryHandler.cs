using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetDocumentById;

namespace PROManagementSystem.Application.Client.Queries.GetMyDocumentById;

public sealed class GetMyDocumentByIdQueryHandler
    : IRequestHandler<GetMyDocumentByIdQuery, GetDocumentByIdResponseDto?>
{
    private readonly IDocumentReadRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetMyDocumentByIdQueryHandler(
        IDocumentReadRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GetDocumentByIdResponseDto?> Handle(
        GetMyDocumentByIdQuery request,
        CancellationToken cancellationToken)
    {
        var clientCompanyId = _currentUser.ClientCompanyId;

        if (!clientCompanyId.HasValue)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        // The tenant restriction runs inside the SQL query, so an
        // out-of-company document resolves to null -> 404.
        return await _repository.GetByIdForCompanyAsync(
            clientCompanyId.Value,
            request.DocumentId,
            cancellationToken);
    }
}