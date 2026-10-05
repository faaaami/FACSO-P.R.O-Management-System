using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeDocuments;

namespace PROManagementSystem.Application.Client.Queries.GetMyEmployeeDocuments;

public sealed class GetMyEmployeeDocumentsQueryHandler
    : IRequestHandler<
        GetMyEmployeeDocumentsQuery,
        GetEmployeeDocumentsResponseDto>
{
    private readonly IEmployeeDocumentReadRepository _repository;
    private readonly IEmployeeReadRepository _scopeRepository;
    private readonly ICurrentUserService _currentUser;

    public GetMyEmployeeDocumentsQueryHandler(
        IEmployeeDocumentReadRepository repository,
        IEmployeeReadRepository scopeRepository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _scopeRepository = scopeRepository;
        _currentUser = currentUser;
    }

    public async Task<GetEmployeeDocumentsResponseDto> Handle(
        GetMyEmployeeDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        var clientCompanyId = _currentUser.ClientCompanyId;

        if (!clientCompanyId.HasValue)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        // Strict company-scoped scope check on the parent employee.
        // Out-of-scope or nonexistent parent -> 404, never an empty list.
        var employeeInCompany =
            await _scopeRepository.IsEmployeeInCompanyAsync(
                clientCompanyId.Value,
                request.EmployeeId,
                cancellationToken);

        if (!employeeInCompany)
        {
            throw new KeyNotFoundException(
                "Employee was not found.");
        }

        return await _repository.GetByEmployeeIdForCompanyAsync(
            clientCompanyId.Value,
            request.EmployeeId,
            cancellationToken);
    }
}