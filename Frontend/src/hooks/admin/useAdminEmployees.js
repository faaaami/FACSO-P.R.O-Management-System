import { useQuery } from '@tanstack/react-query'
import {
  getAdminEmployeeById,
  getAdminEmployeeDocuments,
  getAdminEmployeeTimeline,
  getAdminEmployees,
  getAdminExpiringEmployees,
} from '../../api/admin/employees'

/**
 * Paginated Admin employee list.
 * Backing query GET /api/v1/admin/employees
 * Params: { clientId?, entityId?, isActive?, page = 1, pageSize = 20 }
 *
 * KNOWN LIMITATION: the endpoint accepts no text search, so the Admin Employees
 * page cannot offer server-side search. Filtering is limited to company, entity
 * and active state.
 */
export function useAdminEmployees(params = {}) {
  const clientId = params?.clientId
  const entityId = params?.entityId
  const isActive = params?.isActive
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20

  const query = useQuery({
    queryKey: ['admin', 'employees', { clientId, entityId, isActive, page, pageSize }],
    queryFn: () => getAdminEmployees({ clientId, entityId, isActive, page, pageSize }),
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
    // Exposed so a list can tell a first load (isLoading) from a background
    // refetch (isFetching && !isLoading) and keep showing rows during the latter.
    // Purely additive: existing consumers destructure only what they need.
    isFetching: query.isFetching,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}

/**
 * Single employee.
 * Backing query GET /api/v1/admin/employees/{employeeId}
 * Query: includeDeleted (false)
 */
export function useAdminEmployee(employeeId, params = {}) {
  const includeDeleted = params?.includeDeleted ?? false

  const query = useQuery({
    queryKey: ['admin', 'employee', employeeId, { includeDeleted }],
    queryFn: () => getAdminEmployeeById(employeeId, { includeDeleted }),
    enabled: Boolean(employeeId),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    loading: query.isLoading,
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}

/**
 * Documents held against one employee.
 * Backing query GET /api/v1/admin/employees/{employeeId}/documents
 * Query: includeDeleted (false)
 * Returns { items, totalCount } â€” UNPAGINATED. There is no page or pageSize on
 * this route, so no pager may be built from it and totalCount is a plain row
 * count, not a page count.
 *
 * The route 404s for a soft-deleted employee regardless of includeDeleted, so a
 * failure here is a real "this employee's documents are not readable" signal and
 * the section must not present it as an empty document list.
 */
export function useAdminEmployeeDocuments(employeeId, params = {}) {
  const includeDeleted = params?.includeDeleted ?? false

  const query = useQuery({
    queryKey: ['admin', 'employee-documents', employeeId, { includeDeleted }],
    queryFn: () => getAdminEmployeeDocuments(employeeId, { includeDeleted }),
    enabled: Boolean(employeeId),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    items: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
    loading: query.isLoading,
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}

/**
 * Audit trail for one employee.
 * Backing query GET /api/v1/admin/employees/{employeeId}/timeline
 * Query: page (1), pageSize (20)
 * Returns { employeeId, items, page, pageSize, totalCount }
 */
export function useAdminEmployeeTimeline(employeeId, params = {}) {
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20

  const query = useQuery({
    queryKey: ['admin', 'employee-timeline', employeeId, { page, pageSize }],
    queryFn: () => getAdminEmployeeTimeline(employeeId, { page, pageSize }),
    enabled: Boolean(employeeId),
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

/**
 * Employee documents that are expiring — cross-employee, paginated.
 * Backing query GET /api/v1/admin/employees/expiring
 * Params: { days = 30, page = 1, pageSize = 20, includeExpired = false }
 * Returns { page, pageSize, totalCount, items }
 *   item: ExpiringEmployeeDocumentDto { employeeId, fullName, clientEntityId,
 *     documentId, documentNumber, expiryDate, daysRemaining, status }
 *
 * Every row names its employee, which is the whole reason this route is used in
 * preference to the dashboard's expiry-alerts/employees. That variant returns the
 * document alert DTO with no employee identity, so a row cannot say whose
 * document it is.
 *
 * `days` is clamped to the backend's accepted 1..365 range. The controller
 * returns 400 outside it, and a rejected request is a worse outcome than a
 * clamped one: the list would show an error instead of the 30-day window it
 * claims to be showing.
 *
 * `includeExpired` defaults to false, so the list is forward-looking and "days
 * remaining" is a countdown rather than a measure of how overdue something is.
 *
 * `daysRemaining` is the server's own signed, floored arithmetic and is passed
 * through untouched. Recomputing it from `expiryDate` here could disagree with
 * the server by a day at the boundary and would then contradict the status text
 * printed beside it.
 *
 * The employee name comes from this response and is not re-resolved through
 * useAdminEntityMaps: the row is per DOCUMENT, so an employee with two expiring
 * documents legitimately appears twice, and a per-employee lookup map would
 * either duplicate that or hide the repeat.
 */
export function useAdminExpiringEmployees(params = {}) {
  const rawDays = params?.days ?? 30
  const days = Math.min(365, Math.max(1, Number.isFinite(rawDays) ? rawDays : 30))
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20
  const includeExpired = params?.includeExpired ?? false

  const query = useQuery({
    queryKey: ['admin', 'employees', 'expiring', { days, page, pageSize, includeExpired }],
    queryFn: () => getAdminExpiringEmployees({ days, page, pageSize, includeExpired }),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    items: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
    page: query.data?.page ?? page,
    pageSize: query.data?.pageSize ?? pageSize,
    days,
    includeExpired,
    loading: query.isLoading,
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}
