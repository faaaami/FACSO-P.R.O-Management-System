import axios from 'axios'
import { getAccessToken } from '../auth/storage'

const apiClient = axios.create({
  baseURL: 'https://localhost:7280/api/v1',
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json',
  },
})

apiClient.interceptors.request.use((config) => {
  const token = getAccessToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

export default apiClient