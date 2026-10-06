import { useMutation, useQueryClient } from '@tanstack/react-query'
import { assignAdminTask, setAdminTaskServiceFee, unblockAdminTask } from '../../api/admin/tasks'

/**
 * WRITES for the Admin renewal-task module.
 *
 * Mirrors the Agent split exactly (useAgentTaskActions.js holds updateStatus,
 * block and addStep; useAgentTasks.js holds only reads) for one reason: a file
 * that contains only reads can be trusted not to change anything, and a reviewer
 * checking the mutation boundary does not have to evaluate a mixed file line by
 * line.
 *
 * MUTATION BOUNDARY — THREE, AND EVERY ONE OF THEM IS DELIBERATE.
 * Assignment, the service fee and unblock are the only writes. There is
 * deliberately no wrapper or hook here for status, block, cancel/delete,
 * add-step, or any service-fee INVOICE action. Those routes exist on the
 * backend, but:
 *   - status is forward-only for an Admin and still has no Admin control here;
 *   - block stays Agent-only and is deliberately not reversible by the Agent
 *     who blocked, so it is not an Admin control either;
 *   - cancel is an irreversible soft delete with no restore;
 *   - steps are append-only and there is no edit or delete step route;
 *   - service-fee INVOICE creation requires status Updated, and invoicing is a
 *     billing decision that lives in the billing module, not here.
 * Adding a hook for any of them would be adding a workflow decision, not a
 * missing implementation. Do not add one without that decision.
 *
 * ON WHY UNBLOCK IS HERE BUT BLOCK IS NOT — they are a matched pair, so the
 * asymmetry is a decision and not an oversight. `block` is
 * PATCH /agent/tasks/{id}/block, an Agent acting on their own task, so an Admin
 * wrapper would expose a control the Agent module already owns and the Admin UI
 * has no business duplicating. `unblock` is PATCH /admin/tasks/{id}/unblock, and
 * unblocking is the Admin's decision alone: the handler refuses a non-Admin even
 * for the task they blocked themselves. The Admin surface therefore shows only
 * the half of the pair the Admin is actually responsible for.
 *
 * SPECIFICALLY, THERE IS STILL NO `complete` HERE, AND NOT BECAUSE THE AMOUNT IS
 * UNREACHABLE. Completion would be PATCH /admin/tasks/{taskId}/status with
 * `{ status: 'Updated' }`. The gate on that transition is real and unchanged:
 * UpdateRenewalTaskStatusCommandHandler throws ConflictException -> HTTP 409
 * CONFLICT when `ServiceFeeAmount` is null or <= 0, so a completion attempt is a
 * well-formed request that passes validation and then fails 409, not a 400
 * caused by bad input. That gate IS satisfiable — `setServiceFee` below supplies
 * the amount it requires. What is absent is the Admin CONTROL, which is a
 * workflow choice: status is deliberately not an Admin write here (see the
 * mutation boundary above), and that choice is not made in this file.
 *
 * FOR THE RECORD, completion is not inert today. The Agent already drives it
 * through PATCH /agent/tasks/{id}/status, and reaching `Updated` runs a fixed,
 * already-committed chain inside ONE status transaction:
 * UpdateRenewalTaskStatusCommandHandler publishes RenewalTaskCompletedEvent
 * before CommitAsync, CreateServiceFeeInvoiceOnTaskCompletedHandler creates the
 * service-fee invoice from that event, the document expiry and a history row are
 * written, and the post-commit SignalR push is individually guarded. The
 * notification, the SignalR event and the invoice are therefore NOT optional
 * side effects an Admin control would have to choose between — they are the
 * current, shipped behaviour of reaching `Updated` by any route.
 */
export function useAdminTaskActions() {
  const queryClient = useQueryClient()

  const assign = useMutation({
    mutationFn: ({ taskId, staffId }) => assignAdminTask(taskId, { staffId }),
    onSuccess: (_data, variables) => {
      /*
       * Exactly four invalidations, each with a reason:
       *
       * 1. ['admin','tasks'] — prefix match, so every filter combination and
       *    every page of the list refreshes. `assignedStaffId` is on the row, so
       *    the table and the assigned-only count both change.
       * 2. ['admin','task', taskId] — prefix match, so the detail AND both
       *    nested lazy keys ('steps', 'history') refresh together. The History
       *    tab genuinely needs it: the backend writes a history row on every
       *    assignment. The Steps tab is invalidated for free, which is cheaper
       *    than being precise about a key that almost never changes.
       * 3. ['admin','dashboard','task-status-breakdown'] — the assigned-only
       *    status figures come from GET /admin/staff/workload under this key,
       *    and reassignment moves a task between agents, so the per-status split
       *    and the per-agent rows both shift.
       * 4. ['admin','client'] — prefix match. The client service report reads
       *    GET /admin/clients/{id}/service-report under ['admin','client', id,
       *    'service-report'] and lists each task's AssignedStaffName, which comes
       *    from a LEFT JOIN on staff, so reassignment changes it. Without this the
       *    service report shows the previous assignee until its 60s staleTime
       *    expires.
       *
       *    The four counts in that section (total / Updated / Not yet updated /
       *    Invoices on file) are NOT affected by assignment: they group by task
       *    status, and assignment cannot change a status. The prefix is therefore
       *    future-proofing against a status write path, not a live-count fix.
       *
       *    It cannot be narrowed to the one company, because the mutation carries
       *    only { taskId, staffId }. Following the same reasoning as (3) — cheaper
       *    to refresh a slightly broader prefix than to thread a clientId that
       *    this write path does not have.
       *
       * ['admin','entity-map','staff'] is deliberately NOT invalidated:
       * assignment cannot add, remove or rename an Agent, so that map is still
       * correct. The assignee's display name does not need refreshing from it
       * anyway — GetRenewalTaskByIdResponseDto embeds AssignedStaffInfoDto
       * (fullName, email) directly, so the detail refetch in (2) is what
       * updates the name shown in the drawer.
       */
      queryClient.invalidateQueries({ queryKey: ['admin', 'tasks'] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'task', variables.taskId] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'dashboard', 'task-status-breakdown'] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'client'] })
    },
  })

  const setServiceFee = useMutation({
    mutationFn: ({ taskId, amount }) => setAdminTaskServiceFee(taskId, { amount }),
    onSuccess: (_data, variables) => {
      /*
       * ONE invalidation, and the narrowness is deliberate rather than an
       * oversight — every key the assign mutation refreshes is refreshable here
       * for a reason, and three of them are not:
       *
       * 1. ['admin','task', taskId] — prefix match, so the detail AND both
       *    nested lazy keys ('steps', 'history') refresh together. This is the
       *    only genuinely required key:
       *      - the detail, because `serviceFeeAmount` is on it and the Overview
       *        tab must show the figure that was just written;
       *      - the History tab, because the backend writes a history row on
       *        every fee set or change;
       *      - the Steps tab, invalidated for free, which is cheaper than being
       *        precise about a key this write cannot change.
       *
       * NOT INVALIDATED, each for a specific reason:
       *
       * ['admin','tasks'] — the list. RenewalTaskListItemDto genuinely does not
       * carry ServiceFeeAmount (only the detail DTO does), and this write cannot
       * change a status or an assignee, so no field on the row moves. Refreshing
       * it would be a request whose result is guaranteed identical.
       *
       * ['admin','dashboard','task-status-breakdown'] — those six figures group
       * by task status, and setting a fee cannot change a status. The gate that
       * a fee feeds is a prerequisite for a status change, not a status change.
       *
       * ['admin','client'] — the client service report lists each task's
       * AssignedStaffName via a LEFT JOIN on staff. Nothing here touches the
       * assignee. This is the key that WAS genuinely needed for assignment.
       *
       * ['admin','invoices','service-fee'] / ['admin','billing', …] — the
       * invoice copies Amount from this field only at the moment the task
       * completes, and a task in `Updated` is refused by this endpoint outright.
       * So no cached invoice or billing figure can exist for a task whose fee
       * this mutation is allowed to change.
       *
       * ['admin','audit-log'] — this write does append an audit row, but the
       * assign mutation does not invalidate that key either, and matching it
       * here would leave the two writes inconsistent. The audit trail is reached
       * through a separate filtered page whose staleTime governs its freshness.
       */
      queryClient.invalidateQueries({ queryKey: ['admin', 'task', variables.taskId] })
    },
  })

  const unblock = useMutation({
    mutationFn: ({ taskId, unblockReason }) => unblockAdminTask(taskId, { unblockReason }),
    onSuccess: (_data, variables) => {
      /*
       * FOUR invalidations, and unlike setServiceFee every one is load-bearing.
       * Unblock is the only write in this module that actually changes a status,
       * so every key that reports or groups by status has to move with it.
       *
       * 1. ['admin','tasks'] — prefix match, so every filter combination and page
       *    refreshes. This is the key setServiceFee deliberately skips, and the
       *    reason is status: the row carries `status`, `blockedReason` and
       *    `blockedSince`, and all three of them change. It is also why a
       *    status=Blocked filter goes empty after an unblock rather than showing
       *    a stale row.
       * 2. ['admin','task', taskId] — prefix match, so the detail AND both nested
       *    lazy keys ('steps', 'history') refresh together. The detail must, since
       *    blockedReason/blockedSince go null and status becomes Submitted; the
       *    History tab genuinely needs it, because the backend writes a row for
       *    this unblock and the block's original reason is still there beside it.
       *    Steps is invalidated for free, as elsewhere in this file.
       * 3. ['admin','dashboard','task-status-breakdown'] — these figures group by
       *    task status, so a Blocked -> Submitted move changes the blocked count
       *    and the submitted count in one stroke. Skipped by both other mutations
       *    precisely because neither can change a status.
       * 4. ['admin','client'] — prefix match. This one is NOT a guess and NOT
       *    carried over from `assign`: the client service report DTO puts
       *    `Status` on each task row and a `PendingTasks` figure in its summary,
       *    so an unblock changes both. Without this the report would keep
       *    showing the task as blocked until its 60s staleTime expired.
       *
       * NOT INVALIDATED, and each for a reason rather than by omission:
       *
       * ['admin','audit-log'] — this write does append an audit row, but `assign`
       *    does not invalidate that key either and it is the key whose rows change
       *    the most often here. Matching assign keeps the two consistent; the
       *    audit page is reached through its own filtered route and its own
       *    staleTime.
       *
       * ['admin','agent', …] / any Agent notification cache — the backend sends
       *    NO notification and NO SignalR event for an unblock, because the block
       *    it mirrors sends neither. Invalidating a cache that nothing writes
       *    would buy a refetch and a toast for a push that does not exist.
       *
       * ['admin','invoices','service-fee'] — a task in `Blocked` is not a state
       *    the invoice path acts on, and unblocking changes no amount.
       */
      queryClient.invalidateQueries({ queryKey: ['admin', 'tasks'] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'task', variables.taskId] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'dashboard', 'task-status-breakdown'] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'client'] })
    },
  })

  return { assign, setServiceFee, unblock }
}

export default useAdminTaskActions
