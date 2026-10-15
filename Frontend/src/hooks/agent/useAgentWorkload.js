import { useQuery } from '@tanstack/react-query'
import { getAgentWorkload } from '../../api/agent/workload'

/**
 * Loads the current Agent's workload summary.
 * Backing query GET /api/v1/agent/workload
 */
export function useAgentWorkload() {
  const query = useQuery({
    queryKey: ['agent', 'workload'],
    queryFn: () => getAgentWorkload(),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    loading: query.isLoading,
    isLoading: query.isLoading,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}
