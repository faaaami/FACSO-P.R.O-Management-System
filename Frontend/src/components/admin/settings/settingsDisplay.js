/**
 * Display helpers for the Admin Settings module.
 *
 * Every helper here is derived strictly from GetMyProfileResponseDto
 * { id, fullName, email, phone, role, isActive, emailVerified, clientCompanyId }.
 * That DTO has no approval flag, no permissions array and no roles array, so this
 * module deliberately exposes no such concept: GET /api/v1/users/me is a
 * shared `[Authorize]` route that only describes the caller, and adding an
 * "approved" badge or a permission list would invent a capability the backend
 * does not have.
 */

/**
 * `role` is a single scalar enum serialised as a string by the global
 * JsonStringEnumConverter, so it is displayed as text — the same way
 * routes/index.jsx compares `user?.role === 'Admin'`.
 *
 * Validation limits are not here: they live in the zod schemas next to the forms
 * they belong to, so each rule sits with the field it constrains.
 */

/** Role as a display label, never undefined so StatusPill always has a label. */
export function roleLabel(role) {
  if (typeof role !== 'string') {
    return 'Unknown'
  }
  return role.trim() || 'Unknown'
}

/**
 * Account + email verification as a single labelled pair. The booleans are
 * converted to a StatusPill tone string; passing the boolean itself would render
 * a meaningless pill.
 */
export function accountStatuses(profile) {
  return [
    {
      key: 'account',
      label: profile?.isActive ? 'Active' : 'Inactive',
      tone: profile?.isActive ? 'success' : 'neutral',
    },
    {
      key: 'emailVerification',
      label: profile?.emailVerified ? 'Email Verified' : 'Email Not Verified',
      tone: profile?.emailVerified ? 'success' : 'warning',
    },
  ]
}

/** Email as display text; falls back rather than rendering an empty tile. */
export function emailLabel(email) {
  if (typeof email !== 'string') {
    return '—'
  }
  return email.trim() || '—'
}
