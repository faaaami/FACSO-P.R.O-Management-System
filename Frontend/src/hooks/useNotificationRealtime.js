import { useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { toast } from 'sonner'
import { useAuth } from '../auth/AuthContext'
import { getAccessToken } from '../auth/storage'
import { notificationKeyPrefix } from './useNotificationUnreadCount'

/**
 * Hub address. Same origin by default (Vite and nginx proxy /hubs); when the
 * API lives on another origin (VITE_API_BASE_URL), the hub is on that origin.
 */
function hubUrl() {
  const apiBase = import.meta.env.VITE_API_BASE_URL
  return apiBase
    ? new URL('/hubs/notifications', apiBase).toString()
    : '/hubs/notifications'
}

/**
 * Keeps one SignalR connection to the notification hub while a user is signed
 * in. Each "NotificationReceived" push shows a toast and refreshes that role's
 * notification lists and unread count.
 *
 * The connection is keyed on the user, not on the access token: the token is
 * read fresh by accessTokenFactory on every (re)connect, so a silent token
 * refresh does not tear the connection down. A failed connection is not an
 * error for the user - the lists still load normally and refresh on their own.
 */
export function useNotificationRealtime() {
  const { user, isAuthenticated } = useAuth()
  const queryClient = useQueryClient()
  const userId = user?.id
  const prefix = notificationKeyPrefix(user?.role)

  useEffect(() => {
    if (!isAuthenticated || !userId || !prefix) {
      return undefined
    }

    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl(), {
        accessTokenFactory: () => getAccessToken() ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('NotificationReceived', (notification) => {
      queryClient.invalidateQueries({ queryKey: [prefix, 'notifications'] })
      toast(notification?.title ?? 'New notification', {
        description: notification?.message,
      })
    })

    // After a reconnect, anything pushed while offline was missed: reload.
    connection.onreconnected(() => {
      queryClient.invalidateQueries({ queryKey: [prefix, 'notifications'] })
    })

    let stopped = false

    connection.start().catch(() => {
      // Ignored on purpose (see above). Also covers React StrictMode, which
      // stops the first connection while it is still negotiating.
      if (!stopped) {
        queryClient.invalidateQueries({ queryKey: [prefix, 'notifications'] })
      }
    })

    return () => {
      stopped = true
      connection.stop().catch(() => {})
    }
  }, [isAuthenticated, userId, prefix, queryClient])
}
