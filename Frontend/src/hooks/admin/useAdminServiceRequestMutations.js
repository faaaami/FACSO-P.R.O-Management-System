import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  convertAdminServiceRequest,
  rejectAdminServiceRequest,
} from '../../api/admin/serviceRequests'

/**
 * The two Admin decisions on a service request: convert it into a renewal task,
 * or reject it with a reason.
 *
 * Backing endpoints (both `[Authorize(Roles = "Admin")]`):
 *   PATCH /api/v1/service-requests/{id}/convert   no body
 *   PATCH /api/v1/service-requests/{id}/reject    { reason }
 *
 * WHY INVALIDATION IS ENUMERATED rather than blanket-prefixed on ['admin']. This
 * follows useAdminClientMutations and useAdminStaffMutations. The Admin cache holds
 * independently-keyed reads answering different questions, and a decision disturbs
 * only a few of them; a blanket ['admin'] would also refetch the service report, the
 * entity maps and every unrelated Admin list on a single rejection.
 *
 * THE KEYS INVOLVED, each read from the hook that builds it:
 *   ['admin','service-requests', { page, pageSize }]        the paged list
 *   ['admin','service-request', id]                         one request
 *   ['admin','notifications', { page, … }]                  the notification list
 *   ['admin','dashboard','service-requests-preview', {…}]    the dashboard preview
 *
 * BOTH LIST AND DETAIL ARE INVALIDATED BY EVERY DECISION, and for the list it is
 * the list itself that matters: a converted or rejected request has left the open
 * queue, so the paged list must refetch or the row the Admin just acted on would
 * still be presented as awaiting a decision.
 *
 * THE DASHBOARD PREVIEW IS INVALIDATED because it counts service requests and
 * would otherwise keep showing a decided request in its open total. The
 * notification prefix is invalidated on the same reasoning as
 * useAdminNotificationMutations, which already invalidates the bare
 * ['admin','notifications'] prefix for its own writes.
 *
 * RENEWAL TASK READS ARE NOT INVALIDATED BY A REJECTION, and are only invalidated
 * by a conversion — see useConvertAdminServiceRequest. Renewal Tasks is a separate
 * module with its own keys, and this module cannot address the created task by id
 * (see AdminServiceRequestLifecycleSection: the id has no Admin route), so nothing
 * can be prefetched here; the task list refetches on its own staleTime.
 *
 * NOTHING IS SEEDED WITH setQueryData, following useAdminClientMutations. The
 * decision responses carry no secret, but the argument for invalidating still
 * holds: the list is paged, so writing one item into one page's cache would leave
 * the totalCount and the remaining pages describing the pre-decision state. The
 * hook returns the response to the caller so the dialog can report the new renewal
 * task id once, then drop it.
 */
const LIST_KEY = ['admin', 'service-requests']
const NOTIFICATIONS_KEY = ['admin', 'notifications']
const DASHBOARD_SERVICE_REQUESTS_KEY = ['admin', 'dashboard', 'service-requests-preview']

function invalidateDecisionReads(queryClient, serviceRequestId) {
  queryClient.invalidateQueries({ queryKey: LIST_KEY })
  queryClient.invalidateQueries({ queryKey: ['admin', 'service-request', serviceRequestId] })
  queryClient.invalidateQueries({ queryKey: NOTIFICATIONS_KEY })
  queryClient.invalidateQueries({ queryKey: DASHBOARD_SERVICE_REQUESTS_KEY })
}

/**
 * PATCH /api/v1/service-requests/{id}/convert
 *
 * The caller passes only the id: the route takes no body and the document is
 * read from the request, so there is deliberately no `documentId` option here that
 * could be set and silently ignored.
 *
 * ONE-WAY. Both decision handlers accept only a Submitted request and no endpoint
 * reverses either, so this hook exposes no undo and the dialog does not offer one.
 *
 * CONVERSION ALSO INVALIDATES THE TASK READS, unlike a rejection. A conversion
 * writes a new RenewalTask in Submitted status, so the Admin Tasks list gains a row
 * and the dashboard's task-status breakdown gains a Submitted count. This mirrors
 * useAdminTaskActions, which invalidates ['admin','tasks'] and
 * ['admin','dashboard','task-status-breakdown'] for the same reason when a task
 * changes there. `['admin','client']` is deliberately NOT invalidated, for the
 * reason useAdminTaskActions gives: a newly unassigned Submitted task is not
 * reflected on any client-company scoped read there.
 */
export function useConvertAdminServiceRequest() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (serviceRequestId) => convertAdminServiceRequest(serviceRequestId),
    onSuccess: (_data, serviceRequestId) => {
      invalidateDecisionReads(queryClient, serviceRequestId)
      queryClient.invalidateQueries({ queryKey: ['admin', 'tasks'] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'dashboard', 'task-status-breakdown'] })
    },
  })
}

/**
 * PATCH /api/v1/service-requests/{id}/reject
 *
 * `reason` is forwarded to the wrapper, which trims it. The length rule and the
 * "must contain more than whitespace" rule are left entirely to the server: this
 * hook does not pre-check either, so a caller cannot end up showing a local error
 * for a value the server would have accepted.
 */
export function useRejectAdminServiceRequest() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ serviceRequestId, reason }) =>
      rejectAdminServiceRequest(serviceRequestId, reason),
    onSuccess: (_data, { serviceRequestId }) => {
      invalidateDecisionReads(queryClient, serviceRequestId)
    },
  })
}
