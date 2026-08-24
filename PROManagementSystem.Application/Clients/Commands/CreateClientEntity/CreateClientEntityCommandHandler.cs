using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Clients.Commands.CreateClientEntity;

public sealed class CreateClientEntityCommandHandler
    : IRequestHandler<
        CreateClientEntityCommand,
        CreateClientEntityResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateClientEntityCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CreateClientEntityResponseDto> Handle(
        CreateClientEntityCommand request,
        CancellationToken cancellationToken)
    {
        // Parent company must exist and must not be soft-deleted.
        var clientCompany =
            await _context.ClientCompanies
                .FirstOrDefaultAsync(
                    x => x.Id == request.ClientCompanyId,
                    cancellationToken);

        if (clientCompany is null)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        var entity = new ClientEntity
        {
            Id = Guid.NewGuid(),
            ClientCompanyId = clientCompany.Id,
            EntityName = request.EntityName.Trim(),
            TradeLicenseNumber =
                request.TradeLicenseNumber?.Trim(),
            Emirate =
                request.Emirate?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ClientEntities.Add(entity);

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ClientEntityCreated",
            EntityType = "ClientEntity",
            EntityId = entity.Id,
            Description =
                $"Client entity '{entity.EntityName}' was created.",
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.CreatedAt,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new CreateClientEntityResponseDto(
            entity.Id,
            entity.ClientCompanyId,
            entity.EntityName,
            entity.TradeLicenseNumber,
            entity.Emirate,
            entity.IsActive);
    }
}