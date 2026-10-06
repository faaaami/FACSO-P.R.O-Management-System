import apiClient from '../axios'

/**
 * Retrieves the current Agent's own profile.
 * GET /api/v1/agent/me
 */
export async function getAgentMe() {
  const response = await apiClient.get('/agent/me')
  return response.data.data
}

/**
 * Updates the current Agent's own profile (fullName, phone).
 * PATCH /api/v1/agent/me
 */
export async function updateAgentMe(payload) {
  const response = await apiClient.patch('/agent/me', payload)
  return response.data.data
}

/**
 * Changes the current Agent's own password.
 * PATCH /api/v1/agent/me/change-password
 */
export async function changeAgentPassword(payload) {
  const response = await apiClient.patch('/agent/me/change-password', payload)
  return response.data.data
}
