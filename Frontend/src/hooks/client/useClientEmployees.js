import { useQuery } from '@tanstack/react-query'
import { getClientEmployees } from '../../api/client/employees'

export function useClientEmployees(params = {}) {
  return useQuery({
    queryKey: ['client', 'employees', params],
    queryFn: () => getClientEmployees(params),
    staleTime: 30_000,
  })
}