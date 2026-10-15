import { useMutation, useQueryClient } from '@tanstack/react-query'
import { updateMyProfile } from '../../api/client/account'

export function useUpdateClientProfile() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: updateMyProfile,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['client', 'me'] })
    },
  })
}