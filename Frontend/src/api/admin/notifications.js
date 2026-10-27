import apiClient from '../axios'

/**
 * Notifications. Shared `[Authorize]` controller at /api/v1/notifications — the
 * same routes the Client and Agent portals use, scoped server-side to the
 * caller's own notifications via the user id claim.
 *
 * The unread count comes from GET /api/v1/notifications/unread-count, which
 * counts the signed-in user's own unread notifications whatever their role.
 *
 * Sorting IS supported server-side. `sortBy` is whitelisted in the repository to
 * "title", "isread" and created_at (the default for anything unrecognised);
 * `sortDir` accepts "asc" or "desc" (default "desc"). Note the accepted token is
 * `isread` with no separator — "isRead" also lower-cases to a match, but
 * "is_read" silently falls back to created_at ordering.
 *
 * There is no `unreadOnly` filter; "unread first" is achieved with
 * sortBy=isread&sortDir=asc.
 */

/**
 * GET /api/v1/notifications
 * Query: page (1), pageSize (20), sortBy ("title" | "isread" | "createdAt"),
 *   sortDir ("asc" | "desc")
 * Returns { items, page, pageSize, totalCount }
 *   item: NotificationDto { id, title, message, isRead, readAt?, createdAt }
 */
export async function getAdminNotifications(params = {}) {
  const response = await apiClient.get('/notifications', { params })
  return response.data.data
}

/** GET /api/v1/notifications/unread-count — returns a number. */
export async function getAdminUnreadNotificationCount() {
  const response = await apiClient.get('/notifications/unread-count')
  return response.data.data
}

/** PATCH /api/v1/notifications/{id}/read */
export async function markAdminNotificationAsRead(id) {
  const response = await apiClient.patch(`/notifications/${id}/read`)
  return response.data.data
}

/** PATCH /api/v1/notifications/read-all */
export async function markAllAdminNotificationsAsRead() {
  const response = await apiClient.patch('/notifications/read-all')
  return response.data.data
}
