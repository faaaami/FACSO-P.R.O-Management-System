using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Clients.Commands.CreateClientContact;

public sealed class CreateClientContactCommandHandler
    : IRequestHandler<
        CreateClientContactCommand,
        CreateClientContactResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly ICurrentUserService _currentUser;

    public CreateClientContactCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        ICurrentUserService currentUser)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _currentUser = currentUser;
    }

    public async Task<CreateClientContactResponseDto> Handle(
        CreateClientContactCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Verify client company exists
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

        // 2. Normalize the email ONCE and use that normalized value for both
        // the duplicate check and the stored column.
        //
        // The duplicate check previously compared the RAW request value while
        // the row was stored normalized, so a case or whitespace variant slipped
        // past the application check and surfaced as a database unique-constraint
        // failure (500) instead of the intended 409. Comparing against the same
        // normalized form the Staff path already uses closes that gap.
        // Soft-deleted rows stay excluded by the global query filter, so a
        // released email can be reused.
        var normalizedEmail =
            request.Email.Trim().ToLowerInvariant();

        var emailExists =
            await _context.Users
                .AnyAsync(
                    x => x.Email.ToLower() == normalizedEmail,
                    cancellationToken);

        if (emailExists)
        {
            throw new InvalidOperationException(
                "A user with this email already exists.");
        }

        // 3. Hash the Admin-supplied password.
        //
        // The Admin chooses this value and shares it with the client over a
        // channel they trust, so there is no generated credential on this path
        // and none is returned. The plaintext is consumed here exactly once: it
        // goes into the hasher and is never assigned to the entity, never
        // written to the audit description, never logged, and never passed to
        // the invitation email.
        var passwordHash =
            _passwordHasher.Hash(request.Password);

        // 5. Create real User
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            Phone = request.Phone?.Trim(),
            PasswordHash = passwordHash,

            // Important
            Role = UserRole.Client,

            ClientCompanyId =
                request.ClientCompanyId,

            IsActive = true,

            // Admin created the account,
            // but client has not completed onboarding yet.
            IsApproved = false,

            EmailVerified = true,

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ClientContactCreated",
            EntityType = "User",
            EntityId = user.Id,
            Description =
                $"Client contact '{user.FullName}' was created for company {request.ClientCompanyId}.",
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.CreatedAt,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        // 6. Invitation email.
        //
        // The password is deliberately NOT passed. The Admin shares it with the
        // client manually, so transmitting it here would put a live credential
        // on a channel the Admin was told not to use.
        await _emailService.SendClientInvitationAsync(
     user.Email,
     user.FullName,
     cancellationToken);

        return new CreateClientContactResponseDto(
            user.Id,
            user.ClientCompanyId!.Value,
            user.FullName,
            user.Email,
            user.Phone,
            user.Role.ToString(),
            user.IsActive,
            user.IsApproved);
    }
}
