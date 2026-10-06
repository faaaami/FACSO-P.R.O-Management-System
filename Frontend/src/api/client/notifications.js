import apiClient from '../axios'

/**
 * Retrieves the client's notification list.
 * GET /api/v1/client/notifications
 */
export async function getNotifications(params = {}) {
  const response = await apiClient.get('/client/notifications', { params })
  return response.data.data
}

/**
 * Marks a single notification as read.
 * PATCH /api/v1/client/notifications/{id}/read
 */
export async function markNotificationAsRead(id) {
  const response = await apiClient.patch(`/client/notifications/${id}/read`)
  return response.data.data
}

/**
 * Marks all of the client's notifications as read.
 * PATCH /api/v1/client/notifications/read-all
 */
export async function markAllNotificationsAsRead() {
  const response = await apiClient.patch('/client/notifications/read-all')
  return response.data.data
}