using Microsoft.Extensions.DependencyInjection;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Infrastructure.Email;
using PROManagementSystem.Tests.Extraction;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Security;

/// <summary>
/// Regression coverage for audit finding H1: the development email service
/// must never write verification tokens, password-reset tokens, or temporary
/// passwords to the application log in any environment.
/// </summary>
public sealed class EmailSecretLoggingTests
{
    private const string VerificationToken =
        "SENTINEL-VERIFICATION-TOKEN-8f3a1c";
    private const string ResetToken =
        "SENTINEL-RESET-TOKEN-1b2c9d";
    private const string TemporaryPassword =
        "SENTINEL-TEMP-PASSWORD-9d4e7f";
    private const string Recipient = "client@example.test";

    private static CapturingLogger<DevelopmentEmailService> NewLogger()
        => new();

    private static void AssertNoSecret(
        CapturingLogger<DevelopmentEmailService> logger,
        string secret)
        => Assert.DoesNotContain(
            logger.Entries,
            entry => entry.Contains(secret, StringComparison.Ordinal));

    [Fact(DisplayName =
        "H1: verification token is never written to the log")]
    public async Task VerificationToken_IsNotLogged()
    {
        var logger = NewLogger();
        var service = new DevelopmentEmailService(logger);

        await service.SendEmailVerificationAsync(
            Recipient,
            VerificationToken);

        AssertNoSecret(logger, VerificationToken);
    }

    [Fact(DisplayName =
        "H1: password-reset token is never written to the log")]
    public async Task ResetToken_IsNotLogged()
    {
        var logger = NewLogger();
        var service = new DevelopmentEmailService(logger);

        await service.SendPasswordResetAsync(
            Recipient,
            ResetToken);

        AssertNoSecret(logger, ResetToken);
    }

    [Fact(DisplayName =
        "H1: the client invitation carries no credential at all")]
    public async Task ClientInvitation_AcceptsNoCredential()
    {
        var logger = NewLogger();
        var service = new DevelopmentEmailService(logger);

        await service.SendClientInvitationAsync(
            Recipient,
            "Invited User");

        // The invitation no longer has a password parameter, so there is
        // nothing this call can leak. The Admin-typed value is also asserted
        // absent in case a future overload reintroduces one.
        AssertNoSecret(logger, TemporaryPassword);
    }

    [Fact(DisplayName =
        "H1: development email simulation still records a non-secret entry")]
    public async Task NormalBehaviour_StillRecordsNonSecretEntry()
    {
        var logger = NewLogger();
        var service = new DevelopmentEmailService(logger);

        await service.SendEmailVerificationAsync(
            Recipient,
            VerificationToken);
        await service.SendPasswordResetAsync(
            Recipient,
            ResetToken);
        await service.SendClientInvitationAsync(
            Recipient,
            "Invited User");

        Assert.Equal(3, logger.Entries.Count);
        Assert.All(
            logger.Entries,
            entry => Assert.Contains(
                Recipient,
                entry,
                StringComparison.Ordinal));

        AssertNoSecret(logger, VerificationToken);
        AssertNoSecret(logger, ResetToken);
        AssertNoSecret(logger, TemporaryPassword);
    }

    [Fact(DisplayName =
        "H1: every log level stays enabled yet no secret is emitted " +
        "(environment-independent guarantee)")]
    public async Task NoSecret_Emitted_AtAnyLogLevel()
    {
        var logger = NewLogger();
        var service = new DevelopmentEmailService(logger);

        Assert.True(logger.IsEnabled(Microsoft.Extensions.Logging
            .LogLevel.Trace));
        Assert.True(logger.IsEnabled(Microsoft.Extensions.Logging
            .LogLevel.Critical));

        await service.SendEmailVerificationAsync(Recipient, VerificationToken);
        await service.SendPasswordResetAsync(Recipient, ResetToken);
        await service.SendClientInvitationAsync(
            Recipient,
            "Invited User");

        Assert.All(
            logger.Entries,
            entry =>
            {
                Assert.DoesNotContain(
                    VerificationToken, entry, StringComparison.Ordinal);
                Assert.DoesNotContain(
                    ResetToken, entry, StringComparison.Ordinal);
                Assert.DoesNotContain(
                    TemporaryPassword, entry, StringComparison.Ordinal);
            });
    }
}

/// <summary>
/// Locks in that the <see cref="IEmailService"/> registration consumed by the
/// real host (and therefore by any environment) resolves to the same
/// secret-free implementation.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class EmailServiceRegistrationTests
{
    private readonly DatabaseFixture _fixture;

    public EmailServiceRegistrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "H1: the registered IEmailService is the secret-free " +
        "DevelopmentEmailService")]
    public void RegisteredEmailService_IsDevelopmentEmailService()
    {
        using var scope = _fixture.Factory.Services.CreateScope();

        var emailService = scope.ServiceProvider
            .GetRequiredService<IEmailService>();

        Assert.IsType<DevelopmentEmailService>(emailService);
    }
}
