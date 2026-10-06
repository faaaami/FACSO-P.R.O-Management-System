import apiClient from './axios'

export async function checkHealth() {
  const response = await apiClient.get('/health')
  return response.data
}