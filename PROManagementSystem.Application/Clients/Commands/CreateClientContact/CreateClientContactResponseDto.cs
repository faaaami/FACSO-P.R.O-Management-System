namespace PROManagementSystem.Application.Clients.Commands.CreateClientContact;

/// <summary>
/// Account information for a newly created client contact.
/// <para>
/// This deliberately carries NO password field of any kind. The password is
/// chosen by the Admin, who already holds it, so echoing it back would add a
/// disclosure with no benefit. The only place the plaintext exists is the
/// Admin's own request.
/// </para>
/// </summary>
public sealed record CreateClientContactResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    string FullName,
    string Email,
    string? Phone,
    string Role,
    bool IsActive,
    bool IsApproved
);