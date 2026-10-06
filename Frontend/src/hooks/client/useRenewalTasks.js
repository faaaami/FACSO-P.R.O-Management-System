import { useQuery } from '@tanstack/react-query'
import { getRenewalTasks } from '../../api/client/dashboard'

export function useRenewalTasks(params = {}) {
  return useQuery({
    queryKey: ['client', 'renewal-tasks', params],
    queryFn: () => getRenewalTasks(params),
    staleTime: 30_000,
  })
}