import { useQuery } from '@tanstack/react-query'
import { getAgentClients } from '../../api/agent/clients'

/**
 * Loads the Agent-visible, paginated client-companies list.
 * Backing query GET /api/v1/agent/clients
 * Params: { page = 1, pageSize = 20, search? }
 * `search` is server-side and matches company name or trade licence number only;
 * it does not match emirate or any other column.
 * Returns { data, items, totalCount, loading, isLoading, error, isError, refresh }
 *   totalCount is the account-wide total, so it can drive a page count.
 */
export function useAgentClients(params = {}) {
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20
  const search = params?.search

  const query = useQuery({
    queryKey: ['agent', 'clients', { page, pageSize, search }],
    queryFn: () => getAgentClients({ page, pageSize, search }),
    staleTime: 30_000,
    retry: 1,
  })

  const items = query.data?.items ?? []

  return {
    data: query.data,
    items,
    totalCount: query.data?.totalCount ?? 0,
    loading: query.isLoading,
    isLoading: query.isLoading,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}
