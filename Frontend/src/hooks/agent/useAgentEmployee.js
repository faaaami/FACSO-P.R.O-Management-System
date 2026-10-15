import { useQuery } from '@tanstack/react-query'
import { getAgentEmployeeById, getAgentEmployeeDocuments } from '../../api/agent/employees'

/**
 * Loads a single employee in the Agent's task-scoped portfolio.
 * Backing query GET /api/v1/agent/employees/{id}
 * Returns { data, isLoading, isError, error, refresh }.
 *   data: GetEmployeeByIdResponseDto — see api/agent/employees for the full
 *   field list.
 */
export function useAgentEmployee(employeeId) {
  const query = useQuery({
    queryKey: ['agent', 'employee', employeeId],
    queryFn: () => getAgentEmployeeById(employeeId),
    enabled: Boolean(employeeId),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error,
    refresh: query.refetch,
  }
}

/**
 * Loads the documents held by a single employee in the Agent's portfolio.
 * Backing query GET /api/v1/agent/employees/{id}/documents
 * Returns { items, totalCount, isLoading, isError, error, refresh }.
 */
export function useAgentEmployeeDocuments(employeeId) {
  const query = useQuery({
    queryKey: ['agent', 'employee', employeeId, 'documents'],
    queryFn: () => getAgentEmployeeDocuments(employeeId),
    enabled: Boolean(employeeId),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    items: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error,
    refresh: query.refetch,
  }
}
