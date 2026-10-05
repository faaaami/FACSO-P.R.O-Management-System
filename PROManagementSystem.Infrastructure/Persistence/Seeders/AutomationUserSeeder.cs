using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Identity;

namespace PROManagementSystem.Infrastructure.Persistence.Seeders;

/// <summary>
/// Idempotently ensures the renewal-automation system user exists for the
/// configured <c>RenewalAutomation:UserId</c> actor.
///
/// The automated document-expiry job records this actor on
/// <c>renewal_task_histories.ChangedBy</c>, which is a required foreign key
/// to the <c>users</c> table, and on the <c>RenewalTaskCreated</c> audit, so
/// the configured GUID must be a real user row in every environment where
/// the Hangfire job can run. Following the <see cref="MockClientCompanySeeder"/>
/// pattern, the seed is keyed by both the user id and the email natural key,
/// is a no-op when the account already exists, and never deletes or
/// truncates, so repeated boots are safe. The account is seeded with a
/// discarded random password so it can never be signed into.
/// </summary>
public static class AutomationUserSeeder
{
    public const string FullName = "Renewal Automation";

    /// <summary>Same natural key the mock seeder uses for its automation
    /// account, so development and non-development worlds agree.</summary>
    public const string Email = MockClientCompanySeeder.AutomationEmail;

    public static void Seed(
        ApplicationDbContext db,
        Guid automationUserId,
        DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;

        var exists =
            db.Users.IgnoreQueryFilters()
                .Any(x => x.Id == automationUserId)
            || db.Users.IgnoreQueryFilters()
                .Any(x => x.Email == Email);

        if (exists)
        {
            return;
        }

        db.Users.Add(new User
        {
            Id = automationUserId,
            FullName = FullName,
            Email = Email,
            // Login is impossible: the password is random and discarded.
            PasswordHash =
                new BCryptPasswordHasher().Hash(
                    Guid.NewGuid().ToString("N")),
            Role = UserRole.Admin,
            IsActive = true,
            IsApproved = true,
            EmailVerified = true,
            ClientCompanyId = null,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        });

        db.SaveChanges();
    }
}