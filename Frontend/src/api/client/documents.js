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

/**
 * Downloads one of the client's own finalized document files.
 * GET /api/v1/client/documents/{id}/file
 *
 * Scoped to the signed-in client company, using the same company scoping as
 * the client document detail endpoint. The DTOs' `fileUrl` is a relative
 * storage reference that is not browser-retrievable, so an owned file must come
 * from here. Read-only: the client has no upload, extraction or confirmation
 * surface. Returns raw bytes, so `responseType: 'blob'` is required and the
 * caller must revoke the object URL it creates.
 */
export async function getClientDocumentFile(id) {
  const response = await apiClient.get(`/client/documents/${id}/file`, {
    responseType: 'blob',
  })
  return response.data
}