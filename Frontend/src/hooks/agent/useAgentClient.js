import { useQuery } from '@tanstack/react-query'
import { getAgentClientById, getAgentClientEntities } from '../../api/agent/clients'

/**
 * Loads a single Agent-visible client company.
 * Backing query GET /api/v1/agent/clients/{id}
 *
 * Returns null (and isError) when the company does not exist or is outside the
 * caller's task scope, because the backend answers 404 for both.
 *
 * Returns { data, isLoading, error, isError, refresh }
 *   data: GetClientCompanyByIdResponseDto { id, companyName,
 *     tradeLicenseNumber?, phone?, email?, address?, emirate?, isActive,
 *     isDeleted, createdAt, updatedAt }
 */
export function useAgentClient(clientId) {
  const query = useQuery({
    queryKey: ['agent', 'client', clientId],
    queryFn: () => getAgentClientById(clientId),
    enabled: Boolean(clientId),
    staleTime: 60_000,
    retry: 1,
  })

  return {
    data: query.data ?? null,
    isLoading: query.isLoading,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}

/**
 * Loads the legal entities of an Agent-visible client company.
 * Backing query GET /api/v1/agent/clients/{id}/entities
 * Params: { page = 1, pageSize = 20, search = undefined }
 *
 * `search` is honoured server-side (entity name or trade licence number).
 * Results are narrower than the parent company: an entity only appears when the
 * caller's assigned tasks reference a document linked to it.
 *
 * Returns { data, items, totalCount, page, isLoading, error, isError, refresh }
 */
export function useAgentClientEntities(clientId, params = {}) {
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20
  const search = params?.search || undefined

  const query = useQuery({
    queryKey: ['agent', 'client-entities', clientId, { page, pageSize, search }],
    queryFn: () => getAgentClientEntities(clientId, { page, pageSize, search }),
    enabled: Boolean(clientId),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    items: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
    page,
    isLoading: query.isLoading,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}
