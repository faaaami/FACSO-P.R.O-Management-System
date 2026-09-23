import apiClient from '../axios'

/**
 * Retrieves the client's service requests.
 * GET /api/v1/client/service-requests
 */
export async function getClientServiceRequests(params = {}) {
  const response = await apiClient.get('/client/service-requests', { params })
  return response.data.data
}

/**
 * Retrieves a single client service request by id.
 * GET /api/v1/client/service-requests/{id}
 */
export async function getClientServiceRequest(id) {
  const response = await apiClient.get(`/client/service-requests/${id}`)
  return response.data.data
}

/**
 * Creates a new client service request.
 * POST /api/v1/client/service-requests
 */
export async function createClientServiceRequest(payload) {
  const response = await apiClient.post('/client/service-requests', payload)
  return response.data.data
}