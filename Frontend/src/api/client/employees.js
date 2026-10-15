import apiClient from '../axios'

/**
 * Retrieves the client's employees.
 * GET /api/v1/client/employees
 */
export async function getClientEmployees(params = {}) {
  const response = await apiClient.get('/client/employees', { params })
  return response.data.data
}

/**
 * Retrieves a single client employee by id.
 * GET /api/v1/client/employees/{id}
 */
export async function getClientEmployeeById(id) {
  const response = await apiClient.get(`/client/employees/${id}`)
  return response.data.data
}