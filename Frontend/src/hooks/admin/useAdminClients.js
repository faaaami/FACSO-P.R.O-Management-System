import { useQuery } from '@tanstack/react-query'
import {
  getAdminClients,
  getAdminClientById,
  getAdminClientEntities,
  getAdminClientEntityById,
  getAdminClientContacts,
  getAdminClientServiceReport,
} from '../../api/admin/clients'

/**
 * Paginated Admin client-company list.
 * Backing query GET /api/v1/admin/clients
 * Params: { page = 1, pageSize = 20, search?, includeDeleted = false }
 * `search` is server-side and matches company name or trade licence number only.
 * Admin is not restricted to assigned companies, so totalCount is the full count.
 * Returns { data, items, totalCount, page, loading, isLoading, error, isError, refresh }
 */
export function useAdminClients(params = {}) {
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20
  const search = params?.search
  const includeDeleted = params?.includeDeleted ?? false

  const query = useQuery({
    queryKey: ['admin', 'clients', { page, pageSize, search, includeDeleted }],
    queryFn: () => getAdminClients({ page, pageSize, search, includeDeleted }),
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
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}

/**
 * Single Admin client company.
 * Backing query GET /api/v1/admin/clients/{id}
 * 404 => CLIENT_COMPANY_NOT_FOUND
 */
export function useAdminClient(clientId, params = {}) {
  const includeDeleted = params?.includeDeleted ?? false

  const query = useQuery({
    queryKey: ['admin', 'client', clientId, { includeDeleted }],
    queryFn: () => getAdminClientById(clientId, { includeDeleted }),
    enabled: Boolean(clientId),
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

/**
 * Legal entities of one client company.
 * Backing query GET /api/v1/admin/clients/{clientId}/entities
 * Params: { page = 1, pageSize = 20, search?, includeDeleted = false }
 * NOTE: `id` here is a client_entities id, NOT a client_companies id.
 */
export function useAdminClientEntities(clientId, params = {}) {
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20
  const search = params?.search
  const includeDeleted = params?.includeDeleted ?? false

  const query = useQuery({
    queryKey: ['admin', 'client-entities', clientId, { page, pageSize, search, includeDeleted }],
    queryFn: () => getAdminClientEntities(clientId, { page, pageSize, search, includeDeleted }),
    enabled: Boolean(clientId),
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

/**
 * A single legal entity of one client company.
 * Backing query GET /api/v1/admin/clients/{clientId}/entities/{entityId}
 * Params: { includeDeleted = false }
 * 404 => CLIENT_ENTITY_NOT_FOUND, and the route is scoped by BOTH ids so an
 * entity belonging to another company also 404s rather than leaking.
 */
export function useAdminClientEntity(clientId, entityId, params = {}) {
  const includeDeleted = params?.includeDeleted ?? false

  const query = useQuery({
    queryKey: ['admin', 'client-entity', clientId, entityId, { includeDeleted }],
    queryFn: () => getAdminClientEntityById(clientId, entityId, { includeDeleted }),
    enabled: Boolean(clientId) && Boolean(entityId),
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

/**
 * Contacts of one client company.
 * Backing query GET /api/v1/admin/clients/{clientId}/contacts
 * Params: { page = 1, pageSize = 20, search?, includeDeleted = false }
 * `search` matches full name or email.
 *
 * There is deliberately no `role` parameter here: the endpoint does not accept
 * one. `search` is passed through untouched rather than being filtered in the
 * browser, because the backend already filters server-side and paging a
 * client-side filter would silently under-report.
 */
export function useAdminClientContacts(clientId, params = {}) {
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20
  const search = params?.search
  const includeDeleted = params?.includeDeleted ?? false

  const query = useQuery({
    queryKey: ['admin', 'client-contacts', clientId, { page, pageSize, search, includeDeleted }],
    queryFn: () => getAdminClientContacts(clientId, { page, pageSize, search, includeDeleted }),
    enabled: Boolean(clientId),
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

/**
 * Service report for one client company — counts and tasks, no money.
 * Backing query GET /api/v1/admin/clients/{id}/service-report
 * The action accepts optional from/to but the repository ignores both, so this
 * hook sends NEITHER and the report is all-time by construction rather than by
 * a label. See the api/admin/clients.js wrapper for the SQL evidence.
 *
 * NO MONEY IS EXPOSED. The returned object has no `invoices` and no
 * `totalInvoiced`/`totalPaid` at all, so a caller cannot render them by
 * accident. `amount` is the invoice row's only money field and the DTO carries
 * no currency; the two summary totals are summed across retainer and service-fee
 * rows with no currency grouping. `totalInvoices` is a count and is kept.
 *
 * THE TWO SUMMARY COUNTS ARE RENAMED HERE, deliberately.
 *
 *   source field       value here
 *   completedTasks     updatedTasks  — counts only status === "Updated"
 *   pendingTasks       notUpdatedTasks — every OTHER row, computed as
 *                                           tasks.Count - updatedTasks
 *
 * Passing `completedTasks` and `pendingTasks` through would put those names on
 * screen, and both misdescribe the data: an Approved task is counted as
 * "pending", and so is a Blocked one. Renaming at the boundary means no call
 * site can reintroduce the wrong word, and the section can state plainly that
 * "not updated" includes Approved and Blocked.
 *
 * The counts and the task list cannot disagree: the repository computes the
 * summary from the same in-memory lists it returns, with no LIMIT and no second
 * query, so totalTasks is always tasks.length and
 * updatedTasks + notUpdatedTasks === totalTasks.
 *
 * `task.status` is the RenewalTaskStatus name the SQL produced (Submitted,
 * FeePaid, AwaitingApproval, Blocked, Approved, Updated), so the shared task
 * label and tone helpers apply to it unchanged.
 */
export function useAdminClientServiceReport(clientId) {
  const query = useQuery({
    queryKey: ['admin', 'client', clientId, 'service-report'],
    queryFn: () => getAdminClientServiceReport(clientId),
    enabled: Boolean(clientId),
    staleTime: 60_000,
    retry: 1,
  })

  const tasks = Array.isArray(query.data?.tasks) ? query.data.tasks : []
  const summary = query.data?.summary

  return {
    tasks,
    totalTasks: summary?.totalTasks ?? null,
    updatedTasks: summary?.completedTasks ?? null,
    notUpdatedTasks: summary?.pendingTasks ?? null,
    totalInvoices: summary?.totalInvoices ?? null,
    loading: query.isLoading,
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}
