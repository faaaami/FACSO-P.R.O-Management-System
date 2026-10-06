import apiClient from '../axios'

/**
 * Retrieves the authenticated client's company profile.
 * GET /api/v1/client/company
 */
export async function getClientCompany() {
  const response = await apiClient.get('/client/company')
  return response.data.data
}

/**
 * Retrieves the client's active service contract.
 * GET /api/v1/client/contracts/active
 */
export async function getActiveContract() {
  const response = await apiClient.get('/client/contracts/active')
  return response.data.data
}

/**
 * Retrieves documents expiring within the given window.
 * GET /api/v1/client/documents/expiring
 */
export async function getExpiringDocuments(params = {}) {
  const response = await apiClient.get('/client/documents/expiring', { params })
  return response.data.data
}

/**
 * Retrieves the client's renewal tasks.
 * GET /api/v1/client/tasks
 */
export async function getRenewalTasks(params = {}) {
  const response = await apiClient.get('/client/tasks', { params })
  return response.data.data
}

/**
 * Retrieves the client's service requests.
 * GET /api/v1/client/service-requests
 */
export async function getServiceRequests(params = {}) {
  const response = await apiClient.get('/client/service-requests', { params })
  return response.data.data
}

/**
 * Retrieves the client's payment history.
 * GET /api/v1/client/payments
 */
export async function getPaymentHistory(params = {}) {
  const response = await apiClient.get('/client/payments', { params })
  return response.data.data
}

/**
 * Retrieves the client's unread notification count.
 * GET /api/v1/client/notifications/unread-count
 */
export async function getUnreadNotificationCount() {
  const response = await apiClient.get('/client/notifications/unread-count')
  return response.data.data
}