using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Clients.Commands.DeleteClientCompany;

public sealed class DeleteClientCompanyCommandHandler
    : IRequestHandler<
        DeleteClientCompanyCommand,
        DeleteClientCompanyResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteClientCompanyCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<DeleteClientCompanyResponseDto> Handle(
        DeleteClientCompanyCommand request,
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

        if (clientCompany.IsDeleted)
        {
            throw new InvalidOperationException(
                "Client company is already deleted.");
        }

        var deletedAt = DateTime.UtcNow;

        clientCompany.IsDeleted = true;
        clientCompany.DeletedAt = deletedAt;
        clientCompany.UpdatedAt = deletedAt;

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ClientCompanyDeleted",
            EntityType = "ClientCompany",
            EntityId = clientCompany.Id,
            Description =
                $"Client company '{clientCompany.CompanyName}' was deleted.",
            CreatedAt = deletedAt,
            UpdatedAt = deletedAt,
            IsDeleted = false,
            DeletedAt = null
        };

        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new DeleteClientCompanyResponseDto(
            clientCompany.Id,
            clientCompany.IsDeleted,
            deletedAt);
    }
}