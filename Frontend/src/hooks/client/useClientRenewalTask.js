import { useQuery } from '@tanstack/react-query'
import { getClientRenewalTask } from '../../api/client/renewalTasks'

export function useClientRenewalTask(id) {
  return useQuery({
    queryKey: ['client', 'renewal-task', id],
    queryFn: () => getClientRenewalTask(id),
    enabled: Boolean(id),
    staleTime: 30_000,
  })
}