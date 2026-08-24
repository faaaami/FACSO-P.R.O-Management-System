using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Clients.Commands.UpdateClientCompany;

public sealed class UpdateClientCompanyCommandHandler
    : IRequestHandler<
        UpdateClientCompanyCommand,
        UpdateClientCompanyResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateClientCompanyCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<UpdateClientCompanyResponseDto> Handle(
        UpdateClientCompanyCommand request,
        CancellationToken cancellationToken)
    {
        var clientCompany =
            await _context.ClientCompanies
                .FirstOrDefaultAsync(
                    x => x.Id == request.Id,
                    cancellationToken);

        if (clientCompany is null)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        // Only update fields supplied by the caller.

        if (request.CompanyName is not null)
        {
            clientCompany.CompanyName =
                request.CompanyName.Trim();
        }

        if (request.TradeLicenseNumber is not null)
        {
            clientCompany.TradeLicenseNumber =
                request.TradeLicenseNumber.Trim();
        }

        if (request.Phone is not null)
        {
            clientCompany.Phone =
                request.Phone.Trim();
        }

        if (request.Email is not null)
        {
            clientCompany.Email =
                request.Email.Trim();
        }

        if (request.Address is not null)
        {
            clientCompany.Address =
                request.Address.Trim();
        }

        if (request.Emirate is not null)
        {
            clientCompany.Emirate =
                request.Emirate.Trim();
        }

        if (request.IsActive.HasValue)
        {
            clientCompany.IsActive =
                request.IsActive.Value;
        }

        clientCompany.UpdatedAt = DateTime.UtcNow;

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ClientCompanyUpdated",
            EntityType = "ClientCompany",
            EntityId = clientCompany.Id,
            Description =
                $"Client company '{clientCompany.CompanyName}' was updated.",
            CreatedAt = clientCompany.UpdatedAt,
            UpdatedAt = clientCompany.UpdatedAt,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new UpdateClientCompanyResponseDto(
            clientCompany.Id,
            clientCompany.CompanyName,
            clientCompany.TradeLicenseNumber,
            clientCompany.Phone,
            clientCompany.Email,
            clientCompany.Address,
            clientCompany.Emirate,
            clientCompany.IsActive,
            clientCompany.UpdatedAt);
    }
}