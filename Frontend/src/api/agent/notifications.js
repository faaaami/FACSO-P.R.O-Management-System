import apiClient from '../axios'

/**
 * Retrieves the Agent's notification list.
 * GET /api/v1/agent/notifications
 * Query: page (1), pageSize (20), sortBy (createdAt|title|isRead), sortDir (asc|desc)
 * Returns GetNotificationsResponseDto { items, page, pageSize, totalCount }
 *   item: NotificationDto { id, title, message, isRead, readAt?, createdAt }
 */
export async function getAgentNotifications(params = {}) {
  const response = await apiClient.get('/agent/notifications', { params })
  return response.data.data
}

/**
 * Marks a single Agent notification as read.
 * PATCH /api/v1/agent/notifications/{notificationId}/read
 */
export async function markAgentNotificationAsRead(notificationId) {
  const response = await apiClient.patch(`/agent/notifications/${notificationId}/read`)
  return response.data.data
}

/**
 * Marks all of the Agent's notifications as read.
 * PATCH /api/v1/agent/notifications/read-all
 */
export async function markAllAgentNotificationsAsRead() {
  const response = await apiClient.patch('/agent/notifications/read-all')
  return response.data.data
}
