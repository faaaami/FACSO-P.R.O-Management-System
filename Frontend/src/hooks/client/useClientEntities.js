import { useQuery } from '@tanstack/react-query'
import { getClientEntities } from '../../api/client/entities'

export function useClientEntities(params = {}) {
  return useQuery({
    queryKey: ['client', 'entities', params],
    queryFn: () => getClientEntities(params),
    staleTime: 30_000,
  })
}