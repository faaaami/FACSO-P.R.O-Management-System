using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Auth.Commands.Register;

public sealed class RegisterCommandHandler
    : IRequestHandler<RegisterCommand, RegisterResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _emailService;

    public RegisterCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ICurrentUserService currentUser,
        IEmailService emailService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
        _emailService = emailService;
    }

    public async Task<RegisterResponseDto> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Check whether the email already exists
        var emailExists = await _context.Users
            .AnyAsync(
                x => x.Email == request.Email,
                cancellationToken);

        if (emailExists)
        {
            throw new ConflictException(
                "A user with this email already exists.");
        }

        // 2. Determine role and approval status
        UserRole role;
        bool isApproved;

        if (_currentUser.IsAuthenticated)
        {
            // Only Admin can create staff
            if (!_currentUser.IsAdmin)
            {
                throw new ForbiddenAccessException(
                    "Only an Admin can create staff users.");
            }

            role = UserRole.Agent;
            isApproved = true;
        }
        else
        {
            // Public registration creates a Client
            role = UserRole.Client;
            isApproved = false;
        }

        // 3. Hash the password
        var passwordHash = _passwordHasher.Hash(
            request.Password);

        // 4. Create the user
        var user = new User
        {
            Id = Guid.NewGuid(),

            FullName = request.FullName.Trim(),

            Email = request.Email.Trim().ToLowerInvariant(),

            PasswordHash = passwordHash,

            Phone = request.Phone?.Trim(),

            Role = role,

            IsActive = true,

            IsApproved = isApproved,

            EmailVerified = false,

            CreatedAt = DateTime.UtcNow,

            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);

        // 5. Generate a secure verification token
        var tokenBytes = RandomNumberGenerator.GetBytes(32);

        var verificationToken =
            Convert.ToBase64String(tokenBytes);

        // 6. Hash the token before storing it
        var tokenHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(verificationToken)));

        // 7. Create verification token entity
        var verificationTokenEntity =
            new EmailVerificationToken
            {
                Id = Guid.NewGuid(),

                UserId = user.Id,

                TokenHash = tokenHash,

                ExpiresAt = DateTime.UtcNow.AddHours(24),

                CreatedAt = DateTime.UtcNow,

                UpdatedAt = DateTime.UtcNow
            };

        _context.EmailVerificationTokens.Add(
            verificationTokenEntity);

        // 8. Save User + verification token
        await _context.SaveChangesAsync(
            cancellationToken);

        // 9. Send verification email
        await _emailService.SendEmailVerificationAsync(
            user.Email,
            verificationToken,
            cancellationToken);

        // 10. Return response DTO
        return new RegisterResponseDto(
            user.Id,
            user.FullName,
            user.Email,
            user.Role,
            user.IsApproved,
            user.IsActive);
    }
}