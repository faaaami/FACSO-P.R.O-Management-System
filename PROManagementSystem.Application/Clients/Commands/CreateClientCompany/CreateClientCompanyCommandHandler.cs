using MediatR;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Clients.Commands.CreateClientCompany;

public sealed class CreateClientCompanyCommandHandler
    : IRequestHandler<
        CreateClientCompanyCommand,
        CreateClientCompanyResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateClientCompanyCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CreateClientCompanyResponseDto> Handle(
        CreateClientCompanyCommand request,
        CancellationToken cancellationToken)
    {
        var clientCompany = new ClientCompany
        {
            Id = Guid.NewGuid(),

            CompanyName = request.CompanyName.Trim(),

            TradeLicenseNumber =
                string.IsNullOrWhiteSpace(request.TradeLicenseNumber)
                    ? null
                    : request.TradeLicenseNumber.Trim(),

            Phone =
                string.IsNullOrWhiteSpace(request.Phone)
                    ? null
                    : request.Phone.Trim(),

            Email =
                string.IsNullOrWhiteSpace(request.Email)
                    ? null
                    : request.Email.Trim().ToLowerInvariant(),

            Address =
                string.IsNullOrWhiteSpace(request.Address)
                    ? null
                    : request.Address.Trim(),

            Emirate =
                string.IsNullOrWhiteSpace(request.Emirate)
                    ? null
                    : request.Emirate.Trim(),

            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        clientCompany.CustomerCode =
            await CustomerCodes.NextAsync(_context, cancellationToken);

        _context.ClientCompanies.Add(clientCompany);

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ClientCompanyCreated",
            EntityType = "ClientCompany",
            EntityId = clientCompany.Id,
            Description =
                $"Client company '{clientCompany.CompanyName}' was created.",
            CreatedAt = clientCompany.CreatedAt,
            UpdatedAt = clientCompany.CreatedAt,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(cancellationToken);

        return new CreateClientCompanyResponseDto(
            clientCompany.Id,
            clientCompany.CompanyName,
            clientCompany.TradeLicenseNumber,
            clientCompany.Phone,
            clientCompany.Email,
            clientCompany.Address,
            clientCompany.Emirate,
            clientCompany.IsActive
        );
    }
}