using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Staff.Queries.GetStaffById;

public sealed class GetStaffByIdQueryHandler
    : IRequestHandler<
        GetStaffByIdQuery,
        GetStaffByIdResponseDto?>
{
    private readonly IApplicationDbContext _context;

    public GetStaffByIdQueryHandler(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GetStaffByIdResponseDto?> Handle(
        GetStaffByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Users
            .AsNoTracking()
            .Where(x =>
                x.Id == request.StaffId &&
                x.Role == UserRole.Agent &&
                !x.IsDeleted)
            .Select(x =>
                new GetStaffByIdResponseDto(
                    x.Id,
                    x.FullName,
                    x.Email,
                    x.Phone,
                    x.Role,
                    x.IsActive,
                    x.IsApproved,
                    x.EmailVerified,
                    x.CreatedAt,
                    x.UpdatedAt))
            .FirstOrDefaultAsync(
                cancellationToken);
    }
}