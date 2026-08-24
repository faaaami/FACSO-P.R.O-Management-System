using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.AuditLogs.Queries.GetAuditLog;

public sealed class GetAuditLogQueryHandler
    : IRequestHandler<
        GetAuditLogQuery,
        GetAuditLogResponseDto>
{
    private readonly IAuditLogReadRepository _readRepository;

    public GetAuditLogQueryHandler(
        IAuditLogReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<GetAuditLogResponseDto> Handle(
        GetAuditLogQuery request,
        CancellationToken cancellationToken)
    {
        return await _readRepository.GetListAsync(
            request.EntityType,
            request.EntityId,
            request.UserId,
            request.From,
            request.To,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}
