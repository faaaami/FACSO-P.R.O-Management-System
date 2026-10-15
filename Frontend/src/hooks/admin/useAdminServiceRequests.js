import { useQuery } from '@tanstack/react-query'
import {
  getAdminServiceRequests,
  getAdminServiceRequestById,
} from '../../api/admin/serviceRequests'

/**
 * Paginated Admin service-request list.
 * Backing query GET /api/v1/service-requests
 * Params: { page = 1, pageSize = 20 }
 *
 * APPROVED DECISION Q2: this is an UNFILTERED paged list. The backend accepts
 * only page and pageSize — no status, type, company, date or text filter — so
 * the page must not present server-side filter controls it cannot honour.
 *
 * Client company is GUID-only (ServiceRequestListItemDto.clientCompanyId);
 * resolve it with useAdminEntityMaps.
 */
export function useAdminServiceRequests(params = {}) {
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20

  const query = useQuery({
    queryKey: ['admin', 'service-requests', { page, pageSize }],
    queryFn: () => getAdminServiceRequests({ page, pageSize }),
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
    // Distinguishes a first load from a background refetch, so the list can keep
    // showing its rows during the latter. Additive, and the only change to this
    // hook in this phase.
    isFetching: query.isFetching,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}

/**
 * Single service request.
 * Backing query GET /api/v1/service-requests/{id}
 * 404 => NOT_FOUND
 *
 * GetServiceRequestResponseDto is ServiceRequestListItemDto plus updatedAt and
 * nothing else, so this returns bare ids for the company, employee, entity and
 * document. Resolve the names with useAdminServiceRequestSubjects rather than
 * expecting them here.
 */
export function useAdminServiceRequest(id) {
  const query = useQuery({
    queryKey: ['admin', 'service-request', id],
    queryFn: () => getAdminServiceRequestById(id),
    enabled: Boolean(id),
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
