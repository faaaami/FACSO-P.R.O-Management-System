import { useQuery } from '@tanstack/react-query'
import { getAdminAuditLog } from '../../api/admin/auditLog'

/**
 * Paginated Admin audit log.
 * Backing query GET /api/v1/audit-log
 * Params: { entityType?, entityId?, userId?, from?, to?, page = 1, pageSize = 20 }
 *
 * KNOWN LIMITATIONS: the only filters are EXACT entityType / entityId / userId
 * and a date range. There is no free-text search, no action filter, no sort
 * control, no single-entry detail and no export, so the page must present these
 * as exact-match controls and must not imply a richer query surface.
 */
export function useAdminAuditLog(params = {}) {
  const entityType = params?.entityType
  const entityId = params?.entityId
  const userId = params?.userId
  const from = params?.from
  const to = params?.to
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20

  const query = useQuery({
    queryKey: ['admin', 'audit-log', { entityType, entityId, userId, from, to, page, pageSize }],
    queryFn: () => getAdminAuditLog({ entityType, entityId, userId, from, to, page, pageSize }),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    items: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
    page: query.data?.page ?? page,
    pageSize: query.data?.pageSize ?? pageSize,
    loading: query.isLoading,
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}
