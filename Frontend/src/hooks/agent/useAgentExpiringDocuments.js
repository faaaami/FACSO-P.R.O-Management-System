import { useQuery } from '@tanstack/react-query'
import { getAgentExpiringDocuments } from '../../api/agent/documents'

/**
 * Loads documents expiring within a fixed window, scoped to the Agent's
 * task-linked document portfolio (a document is only in scope when it is the
 * document of a non-deleted renewal task assigned to the signed-in Agent).
 *
 * Backing query GET /api/v1/agent/documents/expiring
 * Params: { days (1..365, default 30; the UI offers 30/60/90), page = 1,
 *           pageSize = 20, includeExpired = false }
 *
 * `includeExpired` is supported by the endpoint. It is opt-in and defaults to
 * false so the panel keeps its current "expiring ahead" semantics; enabling it
 * also returns already-expired documents, which the backend reports with
 * status 'Expired'. daysRemaining is the backend's own signed, floored
 * arithmetic - positive means days until expiry, negative means days since
 * expiry - so expiry state must be read from `status` and never inferred from
 * the sign of daysRemaining.
 *
 * Returns { data, items, totalCount, page, pageSize, loading, isLoading,
 *   error, isError, refresh }
 *   item: ExpiringDocumentListItemDto { id, clientEntityId?, employeeId?, type,
 *     documentNumber, issueDate?, expiryDate?, fileName?, isActive, isDeleted,
 *     daysRemaining, status }
 */
export function useAgentExpiringDocuments(params = {}) {
  const days = params?.days ?? 30
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20
  const includeExpired = params?.includeExpired ?? false

  const query = useQuery({
    queryKey: [
      'agent',
      'documents',
      'expiring',
      { days, page, pageSize, includeExpired },
    ],
    queryFn: () =>
      getAgentExpiringDocuments({ days, page, pageSize, includeExpired }),
    staleTime: 30_000,
    retry: 1,
  })

  const items = query.data?.items ?? []

  return {
    data: query.data,
    items,
    totalCount: query.data?.totalCount ?? 0,
    page: query.data?.page ?? page,
    pageSize: query.data?.pageSize ?? pageSize,
    loading: query.isLoading,
    isLoading: query.isLoading,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}
