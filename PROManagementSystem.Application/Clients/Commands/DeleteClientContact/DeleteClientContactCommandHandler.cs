using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Clients.Commands.DeleteClientContact;

public sealed class DeleteClientContactCommandHandler
    : IRequestHandler<
        DeleteClientContactCommand,
        DeleteClientContactResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteClientContactCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<DeleteClientContactResponseDto> Handle(
        DeleteClientContactCommand request,
        CancellationToken cancellationToken)
    {
        var contact = await _context.Users
            .FirstOrDefaultAsync(
                x =>
                    x.Id == request.ContactId &&
                    x.ClientCompanyId == request.ClientCompanyId &&
                    x.Role == UserRole.Client,
                cancellationToken);

        if (contact is null)
        {
            throw new KeyNotFoundException(
                "Client contact was not found.");
        }

        contact.IsActive = false;
        contact.IsDeleted = true;
        contact.DeletedAt = DateTime.UtcNow;
        contact.UpdatedAt = DateTime.UtcNow;

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ClientContactDeleted",
            EntityType = "User",
            EntityId = contact.Id,
            Description =
                $"Client contact '{contact.FullName}' was deleted.",
            CreatedAt = contact.DeletedAt!.Value,
            UpdatedAt = contact.DeletedAt!.Value,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new DeleteClientContactResponseDto(
            contact.Id,
            contact.IsDeleted,
            contact.IsActive,
            contact.DeletedAt!.Value);
    }
}