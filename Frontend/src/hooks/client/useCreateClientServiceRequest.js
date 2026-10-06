import { useMutation, useQueryClient } from '@tanstack/react-query'
import { createClientServiceRequest } from '../../api/client/serviceRequests'

export function useCreateClientServiceRequest() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: createClientServiceRequest,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['client', 'service-requests'] })
    },
  })
}