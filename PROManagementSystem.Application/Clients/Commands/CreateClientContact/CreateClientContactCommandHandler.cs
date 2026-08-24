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

        // 2. Check whether email is already registered
        var emailExists =
            await _context.Users
                .AnyAsync(
                    x => x.Email == request.Email,
                    cancellationToken);

        if (emailExists)
        {
            throw new InvalidOperationException(
                "A user with this email already exists.");
        }

        // 3. Generate temporary password
        var temporaryPassword =
            GenerateTemporaryPassword();

        // 4. Hash password
        var passwordHash =
            _passwordHasher.Hash(temporaryPassword);

        // 5. Create real User
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
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

            EmailVerified = false,

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

        // 6. Stub invitation email
        await _emailService.SendClientInvitationAsync(
     user.Email,
     user.FullName,
     temporaryPassword,
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

    private static string GenerateTemporaryPassword()
    {
        return $"PRO@{Random.Shared.Next(100000, 999999)}";
    }
}