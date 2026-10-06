import { useQuery } from '@tanstack/react-query'
import { getAgentMe } from '../../api/agent/me'

export function useAgentProfile() {
  return useQuery({
    queryKey: ['agent', 'me'],
    queryFn: getAgentMe,
    staleTime: 30_000,
    retry: 1,
  })
}
