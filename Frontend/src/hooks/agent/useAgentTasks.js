import { useQuery } from '@tanstack/react-query'
import { getAgentTasks } from '../../api/agent/tasks'

/**
 * Loads the paginated renewal-task feed for the current Agent.
 * Backing query GET /api/v1/agent/tasks
 * Params: { page = 1, pageSize = 20, status? }
 */
export function useAgentTasks(params = {}) {
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20

  const query = useQuery({
    queryKey: ['agent', 'tasks', { page, pageSize, status: params?.status }],
    queryFn: () => getAgentTasks({ page, pageSize, status: params?.status }),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    items: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
    loading: query.isLoading,
    isLoading: query.isLoading,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}
