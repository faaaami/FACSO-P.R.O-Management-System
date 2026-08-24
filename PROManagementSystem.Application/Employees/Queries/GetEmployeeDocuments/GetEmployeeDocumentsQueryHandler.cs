using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Employees.Queries.GetEmployeeDocuments;

public sealed class GetEmployeeDocumentsQueryHandler
    : IRequestHandler<
        GetEmployeeDocumentsQuery,
        GetEmployeeDocumentsResponseDto>
{
    private readonly IEmployeeDocumentReadRepository _repository;

    public GetEmployeeDocumentsQueryHandler(
        IEmployeeDocumentReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetEmployeeDocumentsResponseDto> Handle(
        GetEmployeeDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetByEmployeeIdAsync(
            request.EmployeeId,
            request.IncludeDeleted,
            cancellationToken);
    }
}