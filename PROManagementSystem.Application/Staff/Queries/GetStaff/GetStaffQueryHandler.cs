using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Staff.Queries.GetStaff;

public sealed class GetStaffQueryHandler
    : IRequestHandler<
        GetStaffQuery,
        GetStaffResponseDto>
{
    private readonly IApplicationDbContext _context;

    public GetStaffQueryHandler(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GetStaffResponseDto> Handle(
        GetStaffQuery request,
        CancellationToken cancellationToken)
    {
        var query =
            _context.Users
                .AsNoTracking()
                .Where(x =>
                    x.Role == UserRole.Agent &&
                    !x.IsDeleted);

        if (!request.IncludeInactive)
        {
            query = query.Where(
                x => x.IsActive);
        }

        var totalCount =
            await query.CountAsync(
                cancellationToken);

        var items =
            await query
                .OrderByDescending(
                    x => x.CreatedAt)
                .Select(x =>
                    new StaffListItemDto(
                        x.Id,
                        x.FullName,
                        x.Email,
                        x.Phone,
                        x.Role,
                        x.IsActive,
                        x.IsApproved,
                        x.EmailVerified,
                        x.CreatedAt))
                .ToListAsync(
                    cancellationToken);

        return new GetStaffResponseDto(
            items,
            totalCount);
    }
}