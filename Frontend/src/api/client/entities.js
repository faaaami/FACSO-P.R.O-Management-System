import apiClient from '../axios'

/**
 * Retrieves the client's entities.
 * GET /api/v1/client/entities
 */
export async function getClientEntities(params = {}) {
  const response = await apiClient.get('/client/entities', { params })
  return response.data.data
}