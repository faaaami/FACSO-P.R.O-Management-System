import apiClient from '../axios'

/**
 * Retrieves the current client's profile.
 * GET /api/v1/client/me
 */
export async function getMyProfile() {
  const response = await apiClient.get('/client/me')
  return response.data.data
}

/**
 * Updates the current client's profile.
 * PATCH /api/v1/client/me
 */
export async function updateMyProfile(payload) {
  const response = await apiClient.patch('/client/me', payload)
  return response.data.data
}

/**
 * Changes the current client's password.
 * PATCH /api/v1/client/me/change-password
 */
export async function changeMyPassword(payload) {
  const response = await apiClient.patch('/client/me/change-password', payload)
  return response.data.data
}