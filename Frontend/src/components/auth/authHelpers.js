import { useEffect, useState } from 'react'

export const PASSWORD_MIN_LENGTH = 8
export const PASSWORD_MAX_LENGTH = 100

/** Returns an error message, or null when the new password is acceptable. */
export function validateNewPassword(password, confirmation) {
  if (password.length < PASSWORD_MIN_LENGTH) {
    return `Password must be at least ${PASSWORD_MIN_LENGTH} characters.`
  }
  if (password.length > PASSWORD_MAX_LENGTH) {
    return `Password must be ${PASSWORD_MAX_LENGTH} characters or fewer.`
  }
  if (password !== confirmation) {
    return 'Passwords do not match.'
  }
  return null
}

/**
 * Reads the `token` query parameter once, then removes it from the address
 * bar so the single-use link does not linger in history or leak via Referer.
 */
export function useLinkToken() {
  const [token] = useState(() => new URLSearchParams(window.location.search).get('token') ?? '')
  useEffect(() => {
    if (window.location.search.includes('token=')) {
      window.history.replaceState(null, '', window.location.pathname)
    }
  }, [])
  return token
}
