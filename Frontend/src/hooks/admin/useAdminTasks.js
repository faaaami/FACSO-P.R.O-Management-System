import { useQuery } from '@tanstack/react-query'
import {
  getAdminTasks,
  getAdminTaskById,
  getAdminTaskSteps,
  getAdminTaskHistory,
} from '../../api/admin/tasks'

/**
 * READS for the Admin renewal-task module. The single write path
 * (`useAdminTaskActions`) lives in useAdminTaskActions.js, mirroring the Agent
 * split in useAgentTasks.js / useAgentTaskActions.js, so a reader of this file
 * can be certain nothing here mutates.
 *
 * QUERY KEY SHAPE, and why the task-scoped keys are NESTED:
 *
 *   ['admin','tasks',{ status, assignedStaffId, clientCompanyId, page, pageSize }]
 *   ['admin','task', taskId]
 *   ['admin','task', taskId, 'steps']
 *   ['admin','task', taskId, 'history']
 *
 * TanStack Query matches a query key by ARRAY PREFIX, so nesting steps and
 * history under the task id means one
 * `invalidateQueries({ queryKey: ['admin','task', taskId] })` refreshes the
 * detail AND both lazy tabs at once. Flattened keys such as
 * ['admin','task-steps', taskId] would NOT match the ['admin','task'] prefix
 * (the second element differs) and would have to be invalidated separately —
 * which is exactly the trap the Agent module documents in
 * useAgentTaskActions.js. Do not flatten these.
 */

/**
 * Paginated Admin renewal-task list.
 * Backing query GET /api/v1/admin/tasks
 * Params: { status?, assignedStaffId?, clientCompanyId?, page = 1, pageSize = 20 }
 *
 * NOTE: the response is { items, totalCount } — page/pageSize are not echoed.
 *
 * `status` is sent as the enum NAME (e.g. "Blocked"); query-string binding
 * resolves enum names case-insensitively. There is no search, date, entity,
 * employee, type or sort parameter on this endpoint, so none is offered.
 *
 * Assigned staff are GUID-only (RenewalTaskListItemDto.assignedStaffId); resolve
 * them with useAdminEntityMaps rather than rendering a raw id.
 *
 * PER-STATUS COUNTS ARE NOT DERIVED HERE. An earlier version of this comment
 * claimed exact per-status totals came from firing this same endpoint six times
 * with pageSize=1, on the grounds that "the dashboard's own status counters are
 * unreliable". Both halves of that are wrong now:
 *   - GET /admin/staff/workload is verified correct (six correctly named
 *     statuses, Blocked = 4, LEFT JOIN, Agent-scoped) and is served to this
 *     module through useAdminTaskStatusBreakdown, which the Dashboard already
 *     uses — so it is a shared cache entry, not a sixth round trip.
 *   - That endpoint is scoped to ASSIGNED tasks, so its six numbers are
 *     assigned-only by construction. The page labels them that way and shows the
 *     unfiltered `totalCount` from this list beside them, rather than inventing
 *     a global per-status split the backend cannot produce.
 */
export function useAdminTasks(params = {}) {
  const status = params?.status
  const assignedStaffId = params?.assignedStaffId
  const clientCompanyId = params?.clientCompanyId
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20

  const query = useQuery({
    queryKey: ['admin', 'tasks', { status, assignedStaffId, clientCompanyId, page, pageSize }],
    queryFn: () => getAdminTasks({ status, assignedStaffId, clientCompanyId, page, pageSize }),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    items: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
    page,
    pageSize,
    loading: query.isLoading,
    isLoading: query.isLoading,
    // isFetching is separate from isLoading on purpose: it is true again while a
    // background refetch runs, which is what the filter bar's refresh indicator
    // and the table's "stale rows, still updating" state need to tell apart.
    isFetching: query.isFetching,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}

/**
 * Single renewal task.
 * Backing query GET /api/v1/admin/tasks/{id}
 * 404 => NOT_FOUND
 *
 * GetRenewalTaskByIdResponseDto { id, status, documentId, document, clientCompanyId,
 *   assignedStaff, blockedReason, blockedSince, completedAt, stepLogCount,
 *   createdAt, updatedAt } where document is
 *   { id, type, documentNumber, issueDate, expiryDate, fileName } and assignedStaff
 *   is { id, fullName, email } or null.
 *
 * It carries NO employeeId, NO document fileUrl, no service-request and no
 * invoice reference. The company arrives as `clientCompanyId` only, so its name
 * must be resolved through useAdminEntityMaps before any link to /clients can be
 * offered. `fileName` is a bare filename, not a resolvable URL.
 */
export function useAdminTask(taskId) {
  const query = useQuery({
    queryKey: ['admin', 'task', taskId],
    queryFn: () => getAdminTaskById(taskId),
    enabled: Boolean(taskId),
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
 * Renewal-step log for one task.
 * Backing query GET /api/v1/admin/tasks/{taskId}/steps
 * Returns { taskId, items, totalCount }
 *
 * Called ONLY when the Steps tab is first activated, so opening the drawer does
 * not pay for a tab the reader may never open. The key is nested under the task
 * so the assign mutation's single prefix invalidation covers it.
 *
 * `items.length` can be lower than the detail DTO's `stepLogCount` because the
 * count includes soft-deleted step rows. Both are reported as the backend sent
 * them; the caller labels the difference rather than reconciling it.
 */
export function useAdminTaskSteps(taskId) {
  const query = useQuery({
    queryKey: ['admin', 'task', taskId, 'steps'],
    queryFn: () => getAdminTaskSteps(taskId),
    enabled: Boolean(taskId),
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
 * Status/assignment event log for one task.
 * Backing query GET /api/v1/admin/tasks/{taskId}/history
 * Returns { taskId, items, totalCount }
 *   item: RenewalTaskHistoryDto { id, status, changedBy, changedAt, note? }
 *
 * Called ONLY when the History tab is first activated. An assignment event
 * repeats the task's unchanged status, so this is rendered as an append-only
 * event log and never as a state-transition timeline. `changedBy` is a bare
 * Guid: an Admin-authored row cannot be resolved by the Agent-only staff map and
 * stays an explicit short id.
 */
export function useAdminTaskHistory(taskId) {
  const query = useQuery({
    queryKey: ['admin', 'task', taskId, 'history'],
    queryFn: () => getAdminTaskHistory(taskId),
    enabled: Boolean(taskId),
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
