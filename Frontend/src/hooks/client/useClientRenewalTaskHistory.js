import { useQuery } from '@tanstack/react-query'
import { getClientRenewalTaskHistory } from '../../api/client/renewalTasks'

export function useClientRenewalTaskHistory(id) {
  return useQuery({
    queryKey: ['client', 'renewal-task', id, 'history'],
    queryFn: () => getClientRenewalTaskHistory(id),
    enabled: Boolean(id),
    staleTime: 30_000,
  })
}