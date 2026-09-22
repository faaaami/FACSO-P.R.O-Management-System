import apiClient from './axios'

export async function login(credentials) {
  const response = await apiClient.post('/auth/login', credentials)
  return response.data
}