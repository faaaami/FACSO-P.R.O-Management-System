import apiClient from '../axios'

/**
 * Admin renewal-task endpoints. `[Authorize(Roles = "Admin")]` under
 * /api/v1/admin/tasks.
 *
 * `status` arrives string-serialized (JsonStringEnumConverter), e.g. "Approved".
 *
 * ------------------------------------------------------------------
 * STATUS WORKFLOW — corrected against the handler, not inferred.
 * An earlier version of this comment described a strict forward-only chain.
 * That was wrong in two ways, and both mattered to a UI:
 * ------------------------------------------------------------------
 *
 *  1. THE CHAIN IS NOT STRICT FOR AN ADMIN. UpdateRenewalTaskStatusCommandHandler
 *     holds `StatusChain = [Submitted, FeePaid, AwaitingApproval, Approved,
 *     Updated]` and validates an ADMIN request only as
 *     `targetIndex > currentIndex`. So an Admin may SKIP FORWARD (Submitted ->
 *     Approved is legal) and may never move backward or stay put. A non-Admin
 *     Agent is held to `AllowedTransitions`, which is exactly the next
 *     sequential status. The two roles have genuinely different rules.
 *
 *  2. Blocked IS NOT IN THE CHAIN AT ALL, WHICH MAKES IT A DEAD END. Blocked
 *     IS A DEAD END — AND THE EXIT WAS ADDED, NOT IMAGINED.
 *     Because `Blocked` is absent from `StatusChain`, `Array.IndexOf` returns
 *     -1 for a task already in that state, so `currentIndex < 0` rejects every
 *     subsequent /status call ("Cannot transition from Blocked to ..."). The
 *     status endpoint also refuses Blocked as a TARGET ("Blocked status cannot
 *     be set through this endpoint"), so Blocked is reachable only through
 *     PATCH /{taskId}/block. That combination left DELETE as the sole remaining
 *     exit: the Agent could stop work, and the only way back to a live task was
 *     to destroy the task and re-create it by hand.
 *
 *     PATCH /{taskId}/unblock (see `unblockAdminTask` below) is that exit. It is
 *     Admin-only, because unblocking is the Admin's decision and not the
 *     blocking Agent's, and it returns the task to `Submitted` — the start of
 *     the chain — rather than to whatever status it held before it was blocked.
 *     Nothing in the database records that prior status: the block handler
 *     overwrites Status with Blocked and keeps no earlier value, so restoring it
 *     would mean inventing a field. Submitted is the honest target, and it
 *     re-opens the chain: a task that is Submitted again can be driven forward
 *     exactly like a new one.
 *
 *  3. Updated is terminal: any transition FROM it is a 409 ConflictException.
 *     Reaching it additionally requires `ServiceFeeAmount > 0`. That amount is
 *     now settable by an Admin through PATCH /{taskId}/service-fee (see
 *     setAdminTaskServiceFee below), which is what makes completion reachable
 *     and was the reason that endpoint was added. AssignRenewalTaskCommandHandler
 *     rejects assigning an Updated task for the same reason ("Cannot assign a
 *     completed renewal task.").
 *
 *     WHY COMPLETION IS UNREACHABLE, PRECISELY — because the failure mode is not
 *     what a 400 implies. Setting a task to Updated means PATCH
 *     /api/v1/admin/tasks/{taskId}/status with `{ "status": "Updated" }`, and the
 *     validator does NOT block it: UpdateRenewalTaskStatusCommandValidator only
 *     checks that `Status` parses to a defined member and that `Note` is at most
 *     1000 characters. It never inspects a service fee, because the request record
 *     has no service-fee field at all.
 *
 *     The rejection happens one layer deeper, in
 *     UpdateRenewalTaskStatusCommandHandler, which throws ConflictException
 *     ("Cannot complete renewal task: ServiceFeeAmount must be set and greater
 *     than 0.") when the task's ServiceFeeAmount is null or <= 0. So the request
 *     passes validation and is refused as HTTP 409 CONFLICT — a business-rule
 *     guard, not a 400 validation error. Anything that only tests for 4xx will see
 *     the same code and may assume a bad payload; the payload here is well-formed.
 *
 *     THAT DEPENDENCY WAS THE WHOLE PROBLEM, AND IT IS NOW SATISFIABLE.
 *     `ServiceFeeAmount` is a nullable decimal on the entity
 *     (RenewalTask.ServiceFeeAmount, decimal?). It is absent from
 *     GetRenewalTasksResponseDto — the list genuinely does not carry it — but it
 *     IS on GetRenewalTaskByIdResponseDto and, since
 *     setAdminTaskServiceFee was added, is writable through a reachable Admin
 *     path. Before that write path existed the completion gate could only ever
 *     fail, which is why this module shipped without a completion mutation; the
 *     gate itself was never wrong and has not been touched.
 *
 *     None of the above is exposed by this module except unblock, which is the
 *     deliberate exception. The Admin Renewal Tasks page is read-only apart from
 *     assignment, the service fee and unblock: there is still deliberately no
 *     status, block, cancel or add-step wrapper here, and none should be added
 *     without the corresponding UI decision. There is still no completion
 *     mutation, for a different reason than before: it is a separate workflow
 *     decision, not a missing implementation.
 */

/**
 * GET /api/v1/admin/tasks
 * Query: status (RenewalTaskStatus?), assignedStaffId (Guid?), clientCompanyId (Guid?),
 *   page (1), pageSize (20)
 * Returns { items, totalCount } — note: this endpoint does NOT echo page/pageSize.
 *   item: RenewalTaskListItemDto { id, documentId, clientCompanyId,
 *     assignedStaffId?, status, blockedReason?, blockedSince?, completedAt?, createdAt }
 */
export async function getAdminTasks(params = {}) {
  const response = await apiClient.get('/admin/tasks', { params })
  return response.data.data
}

/**
 * GET /api/v1/admin/tasks/{id}
 * 404 => NOT_FOUND
 */
export async function getAdminTaskById(taskId) {
  const response = await apiClient.get(`/admin/tasks/${taskId}`)
  return response.data.data
}

/**
 * GET /api/v1/admin/tasks/{taskId}/history
 * 404 => NOT_FOUND
 *
 * Returns { taskId, items, totalCount }
 *   item: RenewalTaskHistoryDto { id, status, changedBy, changedAt, note? }
 *
 * READ THIS AS AN EVENT LOG, NOT A STATE TIMELINE. Assignment writes a history row
 * carrying the task's UNCHANGED status (AssignRenewalTaskCommandHandler sets
 * `Status = task.Status`) with the note "Task assigned to Agent: <name>.", so
 * consecutive rows legitimately repeat a status. `changedBy` is a bare Guid of
 * the acting USER — for an Admin action that id is an Admin account, which the
 * Agent-only staff map cannot resolve, so such rows stay an explicit short id.
 */
export async function getAdminTaskHistory(taskId) {
  const response = await apiClient.get(`/admin/tasks/${taskId}/history`)
  return response.data.data
}

/**
 * GET /api/v1/admin/tasks/{taskId}/steps
 * 404 => NOT_FOUND
 *
 * Returns { taskId, items, totalCount }
 *   item: RenewalStepLogDto { id, stepName, completedBy, completedAt,
 *     referenceNumber?, proofFileUrl? }
 *
 * COUNT MISMATCH, DELIBERATELY NOT RECONCILED. The detail DTO's `stepLogCount`
 * and this list are produced by different code: the count includes
 * soft-deleted step rows while the list excludes them, so `stepLogCount` can
 * legitimately exceed `items.length`. The UI labels the two figures separately
 * rather than subtracting the difference, because the count is the backend's
 * measurement and a browser-side reconciliation would silently misreport it.
 *
 * `proofFileUrl` is free text supplied when the step was added, not a link
 * verified to resolve. It is rendered as unverified text, never as a download.
 */
export async function getAdminTaskSteps(taskId) {
  const response = await apiClient.get(`/admin/tasks/${taskId}/steps`)
  return response.data.data
}

/**
 * PATCH /api/v1/admin/tasks/{taskId}/assign
 * Body: { staffId } (AssignRenewalTaskRequest)
 * Returns AssignRenewalTaskResponseDto { taskId, staffId, staffName, status,
 *   updatedAt }
 *
 * ONE OF TWO MUTATIONS THIS MODULE PROVIDES. Reassignment is allowed and is
 * reversible, which is why it was chosen as the first write path; the second is
 * setAdminTaskServiceFee below.
 *
 * Server-side guards, all authoritative — the UI surfaces these errors rather
 * than pre-empting or working around them:
 *   - task missing or soft-deleted            => 404
 *   - task.Status == Updated ("completed")    => 400, cannot assign
 *   - staff missing or soft-deleted            => 404
 *   - staff.Role != Agent                      => 400
 *   - !staff.IsActive                          => 400
 *   - the task's linked document is missing    => 404
 *
 * Note the eligibility contract is (role === Agent AND IsActive AND not
 * deleted), which is exactly what GET /admin/staff?includeInactive=false
 * returns, so the picker and the server agree by construction.
 *
 * Side effects the UI must account for: a permanent history row, an
 * audit-log row, a persisted Notification for the Agent, and a SignalR
 * TaskAssigned event. Assignment is therefore consequential and is gated behind
 * a confirmation dialog.
 */
export async function assignAdminTask(taskId, { staffId } = {}) {
  const response = await apiClient.patch(`/admin/tasks/${taskId}/assign`, { staffId })
  return response.data.data
}

/**
 * PATCH /api/v1/admin/tasks/{taskId}/service-fee
 * Body: { amount } (SetRenewalTaskServiceFeeRequest)
 * Returns SetRenewalTaskServiceFeeResponseDto { taskId, serviceFeeAmount, isChange,
 *   status, updatedAt }
 *
 * THE WRITE PATH THAT MAKES COMPLETION REACHABLE. The status endpoint requires
 * `ServiceFeeAmount > 0` before a task may move to `Updated`
 * (UpdateRenewalTaskStatusCommandHandler), and before this endpoint the field was
 * written by no endpoint-reachable code anywhere in the repository — only by dev
 * seeders and test fixtures. That gate is still in force and unchanged; it simply
 * has an input now.
 *
 * `isChange` distinguishes the two cases the UI labels differently: false on the
 * first set, true for every subsequent edit. It is false whenever the task had no
 * prior amount, regardless of what was there before.
 *
 * Server-side guards, all authoritative — the UI surfaces these errors rather
 * than pre-empting or working around them:
 *   - task missing or soft-deleted            => 404
 *   - task.Status == Updated ("completed")    => 409, fee is frozen
 *   - amount <= 0                              => 400
 *   - amount with > 2 decimal places           => 400
 *
 * The amount is bounded by the column's numeric(18,2) scale, NOT by a business
 * maximum: the server rejects a third decimal place rather than silently rounding
 * it, precisely so the figure an Admin reads back is the figure that gets billed.
 * Note that numeric(18,2) is also a whole-digit ceiling, but the server does not
 * enforce it and neither does this module — the same storage rule governs the
 * figure, so a client-side cap would be a second, divergent one.
 *
 * Unlike assignment this sends NO notification and NO SignalR event: the fee is
 * an Admin-side input to the eventual invoice, not an event in the Agent's work.
 * It does write a permanent history row (carrying the task's UNCHANGED status —
 * this is an attribute change, not a transition) and an audit-log row, which is
 * why it is gated behind a confirmation dialog.
 *
 * The write is allowed on any status other than `Updated`, including Blocked.
 * Blocked is outside the status chain, so it is not a state this fee can advance
 * — but refusing the amount there would only re-create the deadlock this
 * endpoint exists to remove, and a fee set on a blocked task still has to be
 * corrected before that task can be completed.
 */
export async function setAdminTaskServiceFee(taskId, { amount } = {}) {
  const response = await apiClient.patch(`/admin/tasks/${taskId}/service-fee`, { amount })
  return response.data.data
}

/**
 * PATCH /api/v1/admin/tasks/{taskId}/unblock
 * Body: { unblockReason } (UnblockRenewalTaskRequest)
 * Returns UnblockRenewalTaskResponseDto { taskId, status, unblockReason, updatedAt }
 *
 * THE EXIT FROM A DEAD END, and the reason it is Admin-only. Block is an Agent
 * action on their own task; unblock reverses it and is the Admin's decision
 * alone, so the route carries [Authorize(Roles = "Admin")] AND the handler
 * re-checks the role. An Agent is refused with 403 even for the task they
 * blocked themselves — unblocking is not a personal undo.
 *
 * BlockedReason and BlockedSince are cleared from the CURRENT state, and the
 * response deliberately does NOT echo them: after a successful unblock they are
 * null, so returning them would invite a caller to render a value that no longer
 * exists. The reason is not lost. The block's own history row keeps the original
 * reason permanently, and the unblock adds a second row carrying this one, so the
 * History tab shows the whole story — which is what the UI points the user to
 * instead of a value that is gone.
 *
 * Server-side guards, all authoritative — the UI surfaces these errors rather
 * than pre-empting or working around them:
 *   - no authenticated user                => 401
 *   - authenticated but not Admin          => 403
 *   - empty / omitted / whitespace reason  => 400
 *   - task missing or soft-deleted         => 404
 *   - task.Status != Blocked               => 409
 *
 * That last guard is what makes a REPEAT unblock a 409 rather than a silent
 * success: the first unblock already moved the task out of Blocked, so the
 * second attempt has nothing left to do. A silent double-success would be worse
 * than an honest refusal, and the message names the real status so the UI can
 * distinguish "wrong state" from "not found" without a second request.
 *
 * Unblocking is consequential, so it is gated behind a confirmation dialog and
 * requires a reason. It writes a permanent history row (Status = Submitted, note
 * prefixed "Unblocked:") and an audit row whose description also names the
 * blocked reason it cleared, and like block it sends NO notification and NO
 * SignalR event — the pair stays symmetric, so an Agent is not told about the
 * unblock by a push while the blocking that paused the task arrived silently.
 */
export async function unblockAdminTask(taskId, { unblockReason } = {}) {
  const response = await apiClient.patch(`/admin/tasks/${taskId}/unblock`, { unblockReason })
  return response.data.data
}
