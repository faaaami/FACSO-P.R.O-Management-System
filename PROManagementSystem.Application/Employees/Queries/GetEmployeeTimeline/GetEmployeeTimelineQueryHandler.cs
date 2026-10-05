using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Employees.Queries.GetEmployeeTimeline;

public sealed class GetEmployeeTimelineQueryHandler
    : IRequestHandler<
        GetEmployeeTimelineQuery,
        GetEmployeeTimelineResponseDto>
{
    private readonly IEmployeeTimelineReadRepository _repository;
    private readonly IApplicationDbContext _context;

    public GetEmployeeTimelineQueryHandler(
        IEmployeeTimelineReadRepository repository,
        IApplicationDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    public async Task<GetEmployeeTimelineResponseDto> Handle(
        GetEmployeeTimelineQuery request,
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

        return await _repository.GetTimelineAsync(
            request.EmployeeId,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}