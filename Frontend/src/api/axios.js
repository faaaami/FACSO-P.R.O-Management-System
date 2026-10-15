import axios from 'axios'
import {
  getAccessToken,
  getRefreshToken,
  setTokens,
  clearAuthSession,
  notifySessionUpdated,
} from '../auth/storage'

const apiClient = axios.create({
  baseURL: 'https://localhost:7280/api/v1',
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json',
  },
})

const AUTH_PATHS = [
  '/auth/login',
  '/auth/register',
  '/auth/refresh',
  '/auth/logout',
  '/auth/verify-email',
  '/auth/forgot-password',
  '/auth/reset-password',
]

function isAuthRequest(url) {
  return AUTH_PATHS.some((path) => (url ?? '').includes(path))
}

apiClient.interceptors.request.use((config) => {
  if (config.skipAuth) {
    return config
  }
  const token = getAccessToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

let refreshPromise = null
let redirectingToLogin = false

function performRefresh() {
  if (refreshPromise) {
    return refreshPromise
  }

  const refreshToken = getRefreshToken()
  if (!refreshToken) {
    clearAuthSession()
    notifySessionUpdated()
    return Promise.reject(new Error('No refresh token available.'))
  }

  refreshPromise = apiClient
    .post('/auth/refresh', { refreshToken }, { skipAuth: true })
    .then((response) => {
      const payload = response?.data?.data ?? response?.data
      if (!payload?.accessToken || !payload?.refreshToken) {
        throw new Error('Invalid refresh response.')
      }
      setTokens({
        accessToken: payload.accessToken,
        refreshToken: payload.refreshToken,
      })
      notifySessionUpdated()
      return payload
    })
    .catch((refreshError) => {
      clearAuthSession()
      notifySessionUpdated()
      throw refreshError
    })
    .finally(() => {
      refreshPromise = null
    })

  return refreshPromise
}

function redirectToLogin() {
  if (redirectingToLogin) {
    return
  }
  redirectingToLogin = true
  if (!window.location.pathname.startsWith('/login')) {
    window.location.assign('/login')
  }
}

apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const { config, response } = error

    if (!config || !response || response.status !== 401) {
      return Promise.reject(error)
    }

    if (config._retried || isAuthRequest(config.url)) {
      return Promise.reject(error)
    }

    try {
      const payload = await performRefresh()
      config._retried = true
      if (config.headers?.set) {
        config.headers.set('Authorization', `Bearer ${payload.accessToken}`)
      } else {
        config.headers.Authorization = `Bearer ${payload.accessToken}`
      }
      return apiClient(config)
    } catch {
      redirectToLogin()
      return Promise.reject(error)
    }
  }
)

export default apiClient