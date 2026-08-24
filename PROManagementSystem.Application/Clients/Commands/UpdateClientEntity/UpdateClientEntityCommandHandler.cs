using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Clients.Commands.UpdateClientEntity;

public sealed class UpdateClientEntityCommandHandler
    : IRequestHandler<
        UpdateClientEntityCommand,
        UpdateClientEntityResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateClientEntityCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<UpdateClientEntityResponseDto> Handle(
        UpdateClientEntityCommand request,
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

        entity.EntityName = request.EntityName.Trim();

        entity.TradeLicenseNumber =
            request.TradeLicenseNumber?.Trim();

        entity.Emirate =
            request.Emirate?.Trim();

        entity.IsActive = request.IsActive;

        entity.UpdatedAt = DateTime.UtcNow;

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ClientEntityUpdated",
            EntityType = "ClientEntity",
            EntityId = entity.Id,
            Description =
                $"Client entity '{entity.EntityName}' was updated.",
            CreatedAt = entity.UpdatedAt,
            UpdatedAt = entity.UpdatedAt,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new UpdateClientEntityResponseDto(
            entity.Id,
            entity.ClientCompanyId,
            entity.EntityName,
            entity.TradeLicenseNumber,
            entity.Emirate,
            entity.IsActive,
            entity.UpdatedAt);
    }
}