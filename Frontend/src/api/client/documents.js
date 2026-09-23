import apiClient from '../axios'

/**
 * Retrieves the client's documents.
 * GET /api/v1/client/documents
 */
export async function getClientDocuments(params = {}) {
  const response = await apiClient.get('/client/documents', { params })
  return response.data.data
}

/**
 * Retrieves a single client document by id.
 * GET /api/v1/client/documents/{id}
 */
export async function getClientDocument(id) {
  const response = await apiClient.get(`/client/documents/${id}`)
  return response.data.data
}

/**
 * Retrieves a client document's scan file reference.
 * GET /api/v1/client/documents/{id}/scan
 */
export async function getClientDocumentScan(id) {
  const response = await apiClient.get(`/client/documents/${id}/scan`)
  return response.data.data
}

/**
 * Retrieves the client's documents expiring within the given window.
 * GET /api/v1/client/documents/expiring
 */
export async function getClientExpiringDocuments(params = {}) {
  const response = await apiClient.get('/client/documents/expiring', { params })
  return response.data.data
}

/**
 * Retrieves the documents of one of the client's employees.
 * GET /api/v1/client/employees/{id}/documents
 */
export async function getClientEmployeeDocuments(employeeId) {
  const response = await apiClient.get(`/client/employees/${employeeId}/documents`)
  return response.data.data
}