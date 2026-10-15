import apiClient from './axios'

export async function login(credentials) {
  const response = await apiClient.post('/auth/login', credentials)
  return response.data
}

/**
 * Revokes the current refresh token server-side.
 * POST /api/v1/auth/logout  body: { refreshToken }
 * The endpoint is [AllowAnonymous] and is already excluded from the 401
 * refresh interceptor, so a failure here never triggers a token refresh loop.
 */
export async function logout(refreshToken) {
  const response = await apiClient.post('/auth/logout', { refreshToken })
  return response.data
}