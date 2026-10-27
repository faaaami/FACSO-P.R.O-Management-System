import { useQuery } from '@tanstack/react-query'
import { useAuth } from '../auth/AuthContext'
import { getAdminUnreadNotificationCount } from '../api/admin/notifications'
import { getAgentUnreadNotificationCount } from '../api/agent/notifications'
import { getUnreadNotificationCount } from '../api/client/dashboard'

const FETCHERS = {
  Admin: getAdminUnreadNotificationCount,
  Agent: getAgentUnreadNotificationCount,
  Client: getUnreadNotificationCount,
}

/**
 * Query-key prefix of a role's notification data: 'admin', 'agent' or
 * 'client'. Every list and count for that role lives under
 * [prefix, 'notifications', ...], so invalidating that prefix refreshes all of
 * them (mark-as-read and live pushes rely on this).
 */
export function notificationKeyPrefix(role) {
  return FETCHERS[role] ? role.toLowerCase() : null
}

/**
 * The signed-in user's unread notification count, for any role. The key sits
 * under the role's notifications prefix, so the existing mark-as-read
 * mutations refresh it without knowing about it.
 */
export function useNotificationUnreadCount() {
  const { user, isAuthenticated } = useAuth()
  const role = user?.role
  const prefix = notificationKeyPrefix(role)

  return useQuery({
    queryKey: [prefix, 'notifications', 'unread-count'],
    queryFn: FETCHERS[role],
    enabled: Boolean(isAuthenticated && prefix),
    staleTime: 30_000,
    // A safety net only: live pushes refresh it immediately.
    refetchInterval: 120_000,
  })
}
