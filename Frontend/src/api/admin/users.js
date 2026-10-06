import apiClient from '../axios'

/**
 * Current-user endpoints backing the Admin Settings page.
 *
 * These live on the shared `[Authorize]` UsersController at /api/v1/users and
 * are therefore available to any authenticated role. They are distinct from the
 * Client portal's /client/me routes in src/api/client/account.js — same
 * capabilities, different controller.
 *
 * There is no Admin-side user or role management surface: no staff reactivate,
 * no hard delete, no role change, and no Admin-account administration.
 */

/**
 * GET /api/v1/users/me
 * Returns GetMyProfileResponseDto { id, fullName, email, phone, role, isActive,
 *   emailVerified, clientCompanyId }
 */
export async function getAdminProfile() {
  const response = await apiClient.get('/users/me')
  return response.data.data
}

/** PATCH /api/v1/users/me — updatable fields are fullName and phone only. */
export async function updateAdminProfile(payload) {
  const response = await apiClient.patch('/users/me', payload)
  return response.data.data
}

/** POST /api/v1/users/change-password */
export async function changeAdminPassword(payload) {
  const response = await apiClient.post('/users/change-password', payload)
  return response.data.data
}
