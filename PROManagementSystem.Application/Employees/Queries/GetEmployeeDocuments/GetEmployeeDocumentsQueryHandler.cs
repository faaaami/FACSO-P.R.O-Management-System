using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Employees.Queries.GetEmployeeDocuments;

public sealed class GetEmployeeDocumentsQueryHandler
    : IRequestHandler<
        GetEmployeeDocumentsQuery,
        GetEmployeeDocumentsResponseDto>
{
    private readonly IEmployeeDocumentReadRepository _repository;
    private readonly IApplicationDbContext _context;

    public GetEmployeeDocumentsQueryHandler(
        IEmployeeDocumentReadRepository repository,
        IApplicationDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    public async Task<GetEmployeeDocumentsResponseDto> Handle(
        GetEmployeeDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        // Nonexistent or soft-deleted parent employee -> 404, not an empty list.
        var employeeExists =
            await _context.Employees
                .AnyAsync(
                    x => x.Id == request.EmployeeId &&
                         !x.IsDeleted,
                    cancellationToken);

        if (!employeeExists)
        {
            throw new KeyNotFoundException(
                "Employee was not found.");
        }

        return await _repository.GetByEmployeeIdAsync(
            request.EmployeeId,
            request.IncludeDeleted,
            cancellationToken);
    }
}