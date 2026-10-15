namespace PROManagementSystem.Application.Clients.Commands.CreateClientContact;

/// <summary>
/// The invited contact. Carries no credential: the contact sets their own
/// password from the invitation email.
/// </summary>
/// <param name="InvitationSent">
/// False when the email could not be delivered; the account exists and the
/// invitation can be resent.
/// </param>
public sealed record CreateClientContactResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    string FullName,
    string Email,
    string? Phone,
    string Role,
    bool IsActive,
    bool IsApproved,
    bool InvitationSent,
    DateTime InvitationExpiresAt
);
