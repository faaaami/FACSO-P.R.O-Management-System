import { useQuery } from '@tanstack/react-query'
import { getAgentEmployees } from '../../api/agent/employees'

/**
 * Loads the Agent-scoped, paginated employee directory.
 * Backing query GET /api/v1/agent/employees
 * Params: { page = 1, pageSize = 20 }
 *
 * The endpoint accepts no filter or search parameter, so none is exposed here.
 * Do not add a `search` param: the controller and the repository SQL both ignore
 * it, and the UI would silently show an unfiltered list.
 *
 * Returns { data, items, totalCount, loading, isLoading, error, isError, refresh }
 *   item: GetEmployeeListItemDto { id, clientEntityId, entityName, fullName,
 *     passportNumber?, nationality?, jobTitle?, hireDate, isActive, isDeleted,
 *     createdAt }
 */
export function useAgentEmployees(params = {}) {
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20

  const query = useQuery({
    queryKey: ['agent', 'employees', { page, pageSize }],
    queryFn: () => getAgentEmployees({ page, pageSize }),
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
