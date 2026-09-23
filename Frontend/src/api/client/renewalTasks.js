import apiClient from '../axios'

/**
 * Retrieves a single client renewal task by id.
 * GET /api/v1/client/tasks/{id}
 */
export async function getClientRenewalTask(id) {
  const response = await apiClient.get(`/client/tasks/${id}`)
  return response.data.data
}

/**
 * Retrieves the history of a client renewal task.
 * GET /api/v1/client/tasks/{id}/history
 */
export async function getClientRenewalTaskHistory(id) {
  const response = await apiClient.get(`/client/tasks/${id}/history`)
  return response.data.data
}