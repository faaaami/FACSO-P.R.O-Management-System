using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Infrastructure.Persistence.Seeders;

public enum AdminBootstrapResult
{
    Created,
    SkippedAdminExists,
    SkippedEmailInUse,
    InvalidEmail,
    PasswordTooShort
}

/// <summary>
/// Creates the first human Admin from ADMIN_EMAIL / ADMIN_PASSWORD when no
/// Admin exists yet. The renewal-automation system account is an Admin row
/// that nobody can sign in to, so it does not count. The seeded Admin must
/// change the password on first sign-in. Idempotent: once any Admin exists,
/// it does nothing, so the variables can stay set (or be removed) safely.
/// </summary>
public static class AdminBootstrapSeeder
{
    public const string EmailVariable = "ADMIN_EMAIL";

    public const string PasswordVariable = "ADMIN_PASSWORD";

    public const int MinimumPasswordLength = 12;

    public static AdminBootstrapResult Seed(
        ApplicationDbContext db,
        IPasswordHasher passwordHasher,
        string email,
        string password,
        Guid automationUserId,
        DateTime? nowUtc = null)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (!MailAddress.TryCreate(normalizedEmail, out var parsed) ||
            parsed.Address != normalizedEmail)
        {
            return AdminBootstrapResult.InvalidEmail;
        }

        if (password.Length < MinimumPasswordLength)
        {
            return AdminBootstrapResult.PasswordTooShort;
        }

        var adminExists = db.Users.Any(
            x => x.Role == UserRole.Admin &&
                 x.Id != automationUserId);

        if (adminExists)
        {
            return AdminBootstrapResult.SkippedAdminExists;
        }

        // Never take over an existing account, even a soft-deleted one.
        var emailInUse = db.Users
            .IgnoreQueryFilters()
            .Any(x => x.Email == normalizedEmail);

        if (emailInUse)
        {
            return AdminBootstrapResult.SkippedEmailInUse;
        }

        var now = nowUtc ?? DateTime.UtcNow;

        var admin = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Administrator",
            Email = normalizedEmail,
            PasswordHash = passwordHasher.Hash(password),
            Role = UserRole.Admin,
            IsActive = true,
            IsApproved = true,
            EmailVerified = true,
            MustChangePassword = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Users.Add(admin);

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = null,
            Action = "AdminBootstrapped",
            EntityType = "User",
            EntityId = admin.Id,
            Description =
                "Initial Admin account was created from environment configuration.",
            CreatedAt = now,
            UpdatedAt = now
        });

        db.SaveChanges();

        return AdminBootstrapResult.Created;
    }
}
