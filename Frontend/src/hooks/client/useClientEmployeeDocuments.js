import { useQuery } from '@tanstack/react-query'
import { getClientEmployeeDocuments } from '../../api/client/documents'

export function useClientEmployeeDocuments(employeeId) {
  return useQuery({
    queryKey: ['client', 'employee', employeeId, 'documents'],
    queryFn: () => getClientEmployeeDocuments(employeeId),
    enabled: Boolean(employeeId),
    staleTime: 30_000,
  })
}