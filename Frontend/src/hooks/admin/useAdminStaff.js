import { useQuery } from '@tanstack/react-query'
import {
  getAdminStaff,
  getAdminStaffById,
  getAdminStaffWorkloadById,
} from '../../api/admin/staff'

/**
 * Paginated Admin staff list.
 * Backing query GET /api/v1/admin/staff
 * Params: { includeInactive = false, page = 1, pageSize = 20 }
 * NOTE: the response is { items, totalCount } — this endpoint does not echo
 * page/pageSize, so the caller keeps the requested page numbers.
 *
 * KNOWN LIMITATION: no server-side text search exists for staff. The list binds
 * only includeInactive/page/pageSize and its SQL has no text predicate, so the
 * Staff page's name/email box filters the CURRENT PAGE in the browser. It is
 * labelled as such rather than presented as a directory-wide search.
 *
 * There is also no role filter and no sort parameter: the query hard-filters
 * `role = Agent` and orders by `created_at DESC, id ASC`.
 *
 * emailVerified is still surfaced per row, but NOT as a warning about
 * Admin-created accounts: the create handler hard-codes EmailVerified = true, so
 * an Agent made through POST /admin/staff can sign in immediately. The flag is
 * only false for an Agent that came through the public Register flow, and for
 * those rows it does gate sign-in, because LoginCommandHandler rejects on
 * !EmailVerified. The column therefore reports the account's real state instead
 * of predicting one.
 */
export function useAdminStaff(params = {}) {
  const includeInactive = params?.includeInactive ?? false
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20

  const query = useQuery({
    queryKey: ['admin', 'staff', { includeInactive, page, pageSize }],
    queryFn: () => getAdminStaff({ includeInactive, page, pageSize }),
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
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}

/**
 * Single staff member.
 * Backing query GET /api/v1/admin/staff/{staffId}
 * 404 => STAFF_NOT_FOUND
 *
 * This is also the only staff query that returns `updatedAt`; the list DTO omits
 * it, so anything showing "last updated" must read from here.
 */
export function useAdminStaffMember(staffId) {
  const query = useQuery({
    queryKey: ['admin', 'staff', staffId],
    queryFn: () => getAdminStaffById(staffId),
    enabled: Boolean(staffId),
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
 * One agent's task workload.
 * Backing query GET /api/v1/admin/staff/{staffId}/workload
 * 404 => STAFF_NOT_FOUND
 *
 * SCOPE — read this before adding it anywhere else. This is the PER-AGENT
 * endpoint and it is the correct one for a detail drawer about one person. The
 * aggregate GET /admin/staff/workload is already the dashboard's single workload
 * source under ['admin','dashboard','task-status-breakdown']; calling it to
 * render one agent's numbers would fetch every agent's rows and then discard
 * all but one. The two are different questions, not two ways to ask the same one.
 *
 * Returns { staffId, activeTaskCount, blockedTaskCount, tasksByStatus }, where
 * tasksByStatus always carries all six renewal-task keys.
 *
 * SEPARATE CACHE ENTRY FROM ['admin','admin','staff', staffId] ON PURPOSE. The
 * workload changes every time a task is assigned, unassigned or moved between
 * statuses, and none of that touches a User row, so folding it into the staff
 * detail key would either make the profile refetch far too often or make the
 * workload too stale to be trusted in a destructive-action gate. A deactivation
 * is refused based on these counts, so a stale workload is a correctness bug,
 * not a cosmetic one. useAdminStaffMutations invalidates it explicitly.
 */
export function useAdminStaffWorkload(staffId) {
  const query = useQuery({
    queryKey: ['admin', 'staff-workload', staffId],
    queryFn: () => getAdminStaffWorkloadById(staffId),
    enabled: Boolean(staffId),
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
