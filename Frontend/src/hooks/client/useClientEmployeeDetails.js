import { useQuery } from '@tanstack/react-query'
import { getClientEmployeeById } from '../../api/client/employees'

export function useClientEmployeeDetails(id) {
  return useQuery({
    queryKey: ['client', 'employees', 'detail', id],
    queryFn: () => getClientEmployeeById(id),
    enabled: Boolean(id),
    staleTime: 30_000,
  })
}