using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Clients.Commands.DeleteClientEntity;

public sealed class DeleteClientEntityCommandHandler
    : IRequestHandler<
        DeleteClientEntityCommand,
        DeleteClientEntityResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteClientEntityCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<DeleteClientEntityResponseDto> Handle(
        DeleteClientEntityCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await _context.ClientEntities
            .FirstOrDefaultAsync(
                x => x.Id == request.EntityId &&
                     x.ClientCompanyId == request.ClientCompanyId,
                cancellationToken);

        if (entity is null)
        {
            throw new KeyNotFoundException(
                "Client entity was not found.");
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ClientEntityDeleted",
            EntityType = "ClientEntity",
            EntityId = entity.Id,
            Description =
                $"Client entity '{entity.EntityName}' was deleted.",
            CreatedAt = entity.DeletedAt!.Value,
            UpdatedAt = entity.DeletedAt!.Value,
            IsDeleted = false,
            DeletedAt = null
        };

        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new DeleteClientEntityResponseDto(
            entity.Id,
            entity.IsDeleted,
            entity.DeletedAt!.Value);
    }
}