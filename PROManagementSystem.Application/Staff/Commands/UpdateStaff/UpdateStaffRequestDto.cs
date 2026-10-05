namespace PROManagementSystem.Api.Controllers.Staff;

/// <summary>
/// Body for <c>PATCH /api/v1/admin/staff/{id}</c>.
/// <para>
/// PATCH is a partial-update verb, so the optional members use nullable types
/// and the handler writes them ONLY when the caller actually supplies a value.
/// Declaring <c>IsApproved</c> as a non-nullable bool made an omitted field
/// bind to <c>false</c> and silently revoked the staff member's approval, which
/// in turn blocks login (LoginCommandHandler refuses an unapproved account).
/// <c>Email</c> is nullable for the same reason: it is normalized on write, so
/// omitting it must preserve the stored value rather than fail model
/// validation or write null.
/// </para>
/// </summary>
public sealed record UpdateStaffRequestDto(
    string FullName,
    string? Email,
    string? Phone,
    bool? IsApproved
);