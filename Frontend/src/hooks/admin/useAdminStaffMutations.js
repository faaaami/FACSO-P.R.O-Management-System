import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  createAdminStaff,
  updateAdminStaff,
  deactivateAdminStaff,
} from '../../api/admin/staff'

/**
 * Admin staff write operations — create, update, deactivate.
 *
 * These are the only three writes the backend exposes for /admin/staff. There is
 * no delete, no role change and no reactivation, and nothing here may imply one.
 *
 * WHY INVALIDATION IS ENUMERATED PER MUTATION rather than blanket-prefixed.
 * The staff module's reads live under three independent cache keys answering
 * three different questions, so a mutation only disturbs the ones it changes:
 *
 *   ['admin','staff', …]                      the paginated list and the by-id
 *                                             profile (prefix-matched, so one
 *                                             invalidate covers both)
 *   ['admin','staff-workload', staffId]       one agent's task counts
 *   ['admin','entity-map','staff']            id -> name map used to label
 *                                             assignees across Admin tables
 *   ['admin','dashboard','task-status-breakdown']
 *                                             the dashboard's workload figures
 *
 * The entity-map case is the one worth justifying, because the existing precedent
 * in useAdminTaskActions deliberately does NOT invalidate it. That is correct
 * there and wrong here, for one reason: assigning a task cannot rename anyone,
 * but editing a staff member CAN. A renamed agent whose map entry is stale shows
 * their old name in the assignee column of the Tasks and Clients tables, and the
 * task drawer's own refetch does not fix it, because
 * GetRenewalTaskByIdResponseDto embeds AssignedStaffInfoDto (fullName, email)
 * directly — it reads the User row, while the map reads a cached list page. The
 * two would disagree, and the map is the one that is wrong.
 *
 * The map pages through everything with a 5 minute staleTime and a hard cap of
 * 1000 staff (see useAdminEntityMaps), so it is the most expensive invalidation
 * in this module. It is still correct to pay it on a rename, and NOT correct to
 * pay it on a create, where the gap is a newly absent name in a list nobody is
 * looking at yet.
 *
 * Deactivation additionally invalidates the dashboard breakdown, because that
 * aggregate is scoped to Agent-role users and a deactivated agent leaves it.
 */

/** Keys a create disturbs: the list and the by-id profile. */
const STAFF_KEYS = [['admin', 'staff']]

/** Keys an update disturbs: adds the cross-module id -> name map. */
const STAFF_AND_NAME_MAP_KEYS = [...STAFF_KEYS, ['admin', 'entity-map', 'staff']]

/** Keys a deactivation disturbs: adds the dashboard's workload aggregate. */
const STAFF_AND_WORKLOAD_KEYS = [
  ...STAFF_AND_NAME_MAP_KEYS,
  ['admin', 'dashboard', 'task-status-breakdown'],
]

/**
 * POST /api/v1/admin/staff
 *
 * The list is invalidated but not the name map: a brand-new agent is not yet
 * referenced by any task, so the one consumer that would mislabel something
 * (the assignee column) has nothing to mislabel yet. See the file header for why
 * that reasoning does not hold for a rename.
 *
 * Nothing is seeded into the cache. The response is a single staff DTO under a
 * key the UI does not read (the drawer is opened by id from a row), so writing
 * it by hand would only create a second copy of the record that could disagree
 * with the list.
 */
export function useCreateAdminStaff() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (payload) => createAdminStaff(payload),
    onSuccess: () => {
      for (const queryKey of STAFF_KEYS) {
        queryClient.invalidateQueries({ queryKey })
      }
    },
  })
}

/**
 * PATCH /api/v1/admin/staff/{staffId}
 *
 * `payload` MUST carry all four DTO fields — fullName, email, phone, isApproved.
 * The endpoint is a full replacement with a PATCH verb, so a partial payload
 * silently writes `isApproved = false` and 500s on a null name or email. The
 * dialog seeds all four from the current row; see AdminStaffFormDialog.
 *
 * `phone` is nullable on the DTO, so clearing the field to null is a real edit
 * and is sent as null rather than omitted.
 */
export function useUpdateAdminStaff() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ staffId, ...payload }) => updateAdminStaff(staffId, payload),
    onSuccess: () => {
      for (const queryKey of STAFF_AND_NAME_MAP_KEYS) {
        queryClient.invalidateQueries({ queryKey })
      }
    },
  })
}

/**
 * DELETE /api/v1/admin/staff/{staffId} — irreversible.
 *
 * The backend sets IsActive = false and offers no route back, and it does NOT
 * check whether the agent still owns open work. The refusal in that case is a
 * FRONTEND guard only; the caller (AdminStaffDeactivateDialog) must have loaded
 * this agent's workload and blocked on it. That is also why the per-agent
 * workload entry is cleared here rather than left behind: keeping stale counts
 * in the cache after the row is gone would let a later attempt pass the safety
 * gate on numbers that no longer describe anything.
 */
export function useDeactivateAdminStaff() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (staffId) => deactivateAdminStaff(staffId),
    onSuccess: (_data, staffId) => {
      for (const queryKey of STAFF_AND_WORKLOAD_KEYS) {
        queryClient.invalidateQueries({ queryKey })
      }

      // Removed rather than invalidated: refetching this agent's workload would
      // now 404, which would park a permanent error in the cache behind a drawer
      // the user has already closed. There is no workload left to show.
      queryClient.removeQueries({ queryKey: ['admin', 'staff-workload', staffId] })
    },
  })
}
