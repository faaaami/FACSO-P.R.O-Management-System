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

/**
 * Exchanges the refresh token for a new token pair. Used after a forced
 * password change so the new access token no longer carries the
 * password-change claim. Returns { accessToken, refreshToken, expiresIn,
 * mustChangePassword }.
 */
export async function refreshSession(refreshToken) {
  const response = await apiClient.post(
    '/auth/refresh',
    { refreshToken },
    { skipAuth: true },
  )
  return response.data.data
}

/** Always resolves with a generic message; it never reveals whether the email exists. */
export async function requestPasswordReset(email) {
  const response = await apiClient.post('/auth/forgot-password', { email }, { skipAuth: true })
  return response.data.data
}

export async function resetPassword({ token, newPassword }) {
  const response = await apiClient.post(
    '/auth/reset-password',
    { token, newPassword },
    { skipAuth: true },
  )
  return response.data.data
}

export async function verifyEmail(token) {
  const response = await apiClient.post('/auth/verify-email', { token }, { skipAuth: true })
  return response.data.data
}

export async function resendVerification(email) {
  const response = await apiClient.post('/auth/resend-verification', { email }, { skipAuth: true })
  return response.data.data
}

/** Returns { email, canSignIn, message }. */
export async function acceptInvitation({ token, newPassword }) {
  const response = await apiClient.post(
    '/auth/accept-invitation',
    { token, newPassword },
    { skipAuth: true },
  )
  return response.data.data
}

/** Works for every role. Body: { currentPassword, newPassword, confirmPassword }. */
export async function changePassword(payload) {
  const response = await apiClient.post('/users/change-password', payload)
  return response.data
}
