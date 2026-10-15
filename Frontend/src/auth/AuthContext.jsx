import { createContext, useContext, useEffect, useState } from 'react'
import {
  login as loginRequest,
  logout as logoutRequest,
  refreshSession,
} from '../api/auth'
import {
  getAccessToken,
  getRefreshToken,
  getUser,
  setAuthSession,
  clearAuthSession,
  SESSION_UPDATED_EVENT,
} from './storage'

const AuthContext = createContext(null)

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [accessToken, setAccessToken] = useState(null)
  const [isInitializing, setIsInitializing] = useState(true)

  useEffect(() => {
    const storedAccessToken = getAccessToken()
    const storedRefreshToken = getRefreshToken()
    const storedUser = getUser()
    if (storedAccessToken && storedRefreshToken && storedUser) {
      setAccessToken(storedAccessToken)
      setUser(storedUser)
    }
    setIsInitializing(false)
  }, [])

  useEffect(() => {
    function handleSessionUpdated() {
      setAccessToken(getAccessToken())
      setUser(getUser())
    }
    window.addEventListener(SESSION_UPDATED_EVENT, handleSessionUpdated)
    return () =>
      window.removeEventListener(SESSION_UPDATED_EVENT, handleSessionUpdated)
  }, [])

  async function login(credentials) {
    const data = await loginRequest(credentials)
    setAuthSession({
      accessToken: data.data.accessToken,
      refreshToken: data.data.refreshToken,
      user: data.data.user,
    })
    setAccessToken(data.data.accessToken)
    setUser(data.data.user)
    return data
  }

  // Clear-first: the local session is torn down synchronously so signing out
  // always works, even with no network. The refresh token is captured before
  // clearing and only then sent to the server so it can be revoked. A failed
  // revocation is intentionally ignored - the local session is already gone.
  async function logout() {
    const refreshToken = getRefreshToken()
    clearAuthSession()
    setAccessToken(null)
    setUser(null)
    if (!refreshToken) {
      return
    }
    try {
      await logoutRequest(refreshToken)
    } catch {
      // Best-effort server-side revocation. Never surface to the user and
      // never leave the client authenticated.
    }
  }

  function updateUser(partial) {
    const mergedUser = { ...getUser(), ...partial }
    setAuthSession({
      accessToken: getAccessToken(),
      refreshToken: getRefreshToken(),
      user: mergedUser,
    })
    setUser(mergedUser)
  }

  // After a forced password change: fetch a token pair without the
  // password-change claim and clear the flag on the stored user.
  async function completePasswordChange() {
    const tokens = await refreshSession(getRefreshToken())
    const mergedUser = { ...getUser(), mustChangePassword: false }
    setAuthSession({
      accessToken: tokens.accessToken,
      refreshToken: tokens.refreshToken,
      user: mergedUser,
    })
    setAccessToken(tokens.accessToken)
    setUser(mergedUser)
    return mergedUser
  }

  const isAuthenticated = Boolean(accessToken && user)

  const value = {
    user,
    accessToken,
    isAuthenticated,
    isInitializing,
    login,
    logout,
    updateUser,
    completePasswordChange,
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}