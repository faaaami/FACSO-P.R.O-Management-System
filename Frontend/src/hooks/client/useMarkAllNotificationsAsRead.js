import { useMutation, useQueryClient } from '@tanstack/react-query'
import { markAllNotificationsAsRead } from '../../api/client/notifications'

export function useMarkAllNotificationsAsRead() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: markAllNotificationsAsRead,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['client', 'notifications'] })
    },
  })
}