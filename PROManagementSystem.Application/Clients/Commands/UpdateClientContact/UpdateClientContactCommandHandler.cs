using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Clients.Commands.UpdateClientContact;

public sealed class UpdateClientContactCommandHandler
    : IRequestHandler<
        UpdateClientContactCommand,
        UpdateClientContactResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateClientContactCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<UpdateClientContactResponseDto> Handle(
        UpdateClientContactCommand request,
        CancellationToken cancellationToken)
    {
        var contact = await _context.Users
            .FirstOrDefaultAsync(
                x =>
                    x.Id == request.ContactId &&
                    x.ClientCompanyId == request.ClientCompanyId &&
                    x.Role == Domain.Enums.UserRole.Client,
                cancellationToken);

        if (contact is null)
        {
            throw new KeyNotFoundException(
                "Client contact was not found.");
        }

        contact.IsActive = request.IsActive;
        contact.UpdatedAt = DateTime.UtcNow;

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ClientContactUpdated",
            EntityType = "User",
            EntityId = contact.Id,
            Description =
                $"Client contact '{contact.FullName}' was updated.",
            CreatedAt = contact.UpdatedAt,
            UpdatedAt = contact.UpdatedAt,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new UpdateClientContactResponseDto(
            contact.Id,
            contact.ClientCompanyId!.Value,
            contact.FullName,
            contact.Email,
            contact.Phone,
            contact.Role,
            contact.IsActive,
            contact.IsApproved,
            contact.UpdatedAt);
    }
}