import { createContext, useContext, useEffect, useState } from 'react'
import { login as loginRequest } from '../api/auth'
import {
  getAccessToken,
  getRefreshToken,
  getUser,
  setAuthSession,
  clearAuthSession,
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

  function logout() {
    clearAuthSession()
    setAccessToken(null)
    setUser(null)
  }

  const isAuthenticated = Boolean(accessToken && user)

  const value = {
    user,
    accessToken,
    isAuthenticated,
    isInitializing,
    login,
    logout,
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