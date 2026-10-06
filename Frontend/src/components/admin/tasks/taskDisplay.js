import {
  RENEWAL_TASK_STATUS_VALUES,
  renewalTaskStatusLabel,
} from '../enumLabels'
import {
  MISSING_VALUE,
  displayText,
  formatDate,
  formatDateTime,
  presentText,
  truncate,
} from '../clients/clientDisplay'

/**
 * Presentation helpers for the Admin Renewal Tasks module.
 *
 * Every field referenced anywhere in this module was read out of the backend
 * response records:
 *
 *   RenewalTaskListItemDto      id, documentId, clientCompanyId, assignedStaffId?,
 *                               status, blockedReason?, blockedSince?, completedAt?,
 *                               createdAt
 *   GetRenewalTaskById…Dto      the above + document, assignedStaff, stepLogCount,
 *                               updatedAt, where
 *                                 document      = { id, type, documentNumber,
 *                                                   issueDate?, expiryDate?, fileName? }
 *                                 assignedStaff = { id, fullName, email } | null
 *                               …plus serviceFeeAmount (decimal?, detail only —
 *                               the list DTO genuinely does not carry it)
 *   RenewalTaskHistoryDto       id, status, changedBy, changedAt, note?
 *   RenewalStepLogDto           id, stepName, completedBy, completedAt,
 *                               referenceNumber?, proofFileUrl?
 *
 * There is NO employeeId, NO document fileUrl, no service-request reference and
 * no invoice reference on any of them. Nothing in this file invents one.
 *
 * Shared primitives (MISSING_VALUE, formatDate, formatDateTime, presentText,
 * displayText, truncate) are imported from the completed Admin Clients module
 * and re-exported here, per the approved decision to import across module
 * folders rather than refactor finished Phase 2 files. They are re-exported
 * rather than reimplemented so this module has ONE import site for formatting,
 * and so every Admin task screen formats a date identically.
 *
 * Nothing in this file encodes workflow rules. There is deliberately no
 * "canAssign(task)" helper, no terminal-state predicate and no transition
 * validator: assignment eligibility is the server's decision, and a client-side
 * restatement of it would be a second, quietly divergent source of truth.
 */

/** Re-exported shared formatting primitives — see the note above. */
export { MISSING_VALUE, displayText, formatDate, formatDateTime, presentText, truncate }

/**
 * The six statuses in enum-ordinal order (Submitted=1 … Updated=6), derived from
 * the verified ordinals rather than written out by hand, so the order used by the
 * summary cards and the status filter is the enum's own and cannot drift from it.
 * Blocked (4) is included: it is a real, reachable state even though it sits
 * outside the status-transition chain.
 */
export const TASK_STATUS_KEYS = Object.keys(RENEWAL_TASK_STATUS_VALUES).sort(
  (a, b) => RENEWAL_TASK_STATUS_VALUES[a] - RENEWAL_TASK_STATUS_VALUES[b],
)

/**
 * APPROVED DESIGN.md TONE MAP — the single source of status colour in this
 * module, used by the table, the drawer header, the overview and the cards.
 *
 *   Submitted / FeePaid / AwaitingApproval -> warning  (work still outstanding)
 *   Blocked                               -> danger    (needs intervention)
 *   Approved / Updated                    -> success   (past the approval gate)
 *
 * This map is LOCAL to Admin Renewal Tasks and intentionally does not touch the
 * Dashboard, which keeps its own divergent tone behaviour. That divergence is
 * recorded rather than fixed here: the Admin Dashboard is a completed Phase 1
 * module, and reconciling the two is a visual-consistency decision for the
 * Dashboard's owner, not a side effect of building this page.
 *
 * Colour is decorative only — every pill and card in this module ships its text
 * label, so status is never communicated by colour alone.
 *
 * An UNRECOGNISED member falls through to 'warning' rather than to a healthy
 * tone: a status the backend adds later must stay visibly "needs attention"
 * instead of silently inheriting the Approved palette.
 */
export const TASK_STATUS_TONE = {
  Submitted: 'warning',
  FeePaid: 'warning',
  AwaitingApproval: 'warning',
  Blocked: 'danger',
  Approved: 'success',
  Updated: 'success',
}

export function taskStatusTone(status) {
  if (status == null) return 'neutral'
  return TASK_STATUS_TONE[status] ?? 'warning'
}

/**
 * Human label for a status, delegated to the shared admin enumLabels helper
 * (RenewalTaskStatus — Submitted=1 … Updated=6) so there is exactly one place
 * that knows the display wording. Not reimplemented here.
 */
export function taskStatusLabel(status) {
  return renewalTaskStatusLabel(status)
}

/**
 * The status a task's step log is reported under, or null when absent. Kept as a
 * pass-through so a caller can render a missing status as the honest dash
 * instead of an invented one.
 */
export function taskStatusText(status) {
  return taskStatusLabel(status) ?? null
}

/**
 * Shortens a GUID to an explicitly UNRESOLVED reference.
 *
 * Two independent things in this module produce bare Guids that the Agent-only
 * staff map cannot resolve: an entity beyond the 1,000-row lookup cap, and an
 * actor who is an Admin rather than an Agent (history `changedBy` and steps
 * `completedBy` record the acting USER, not necessarily an assignee). Neither
 * may be rendered as if it were a name, so the full value always stays in the
 * element's `title` and the caller labels the result as unresolved.
 */
export function shortGuid(value) {
  const text = presentText(value)
  if (!text) return null
  return text.length > 8 ? `${text.slice(0, 8)}…` : text
}

/**
 * The assignee label for a task, preferring the detail endpoint's own denormalized
 * `assignedStaff` and falling back to the entity-map resolver for the list, which
 * carries only `assignedStaffId`.
 *
 * Returns one of three honest shapes, and never a fourth:
 *   null              -> unassigned (assignedStaffId is null): a real state
 *   { name }          -> resolved to a real name
 *   { short, resolved:false } -> present but unresolvable, rendered as a short id
 *
 * An empty or whitespace name is treated as unresolved rather than as a name,
 * because a blank label next to a short id would read as a rendering fault.
 */
export function assigneeDescriptor(assignedStaff, resolveStaff, assignedStaffId) {
  if (assignedStaff?.fullName) {
    return { name: assignedStaff.fullName, email: assignedStaff.email ?? null, resolved: true }
  }

  const fromMap = resolveStaff?.(assignedStaffId ?? assignedStaff?.id)
  if (fromMap?.resolved) {
    return { name: fromMap.name, email: null, resolved: true }
  }

  const id = assignedStaffId ?? assignedStaff?.id
  if (!id) return null

  return { name: shortGuid(id), resolved: false, id }
}

/**
 * `proofFileUrl` is free text typed in when a step was added. It is not a link
 * the backend has verified resolves, and there is no Admin download route for a
 * task step, so it is only ever shown as inert reference text.
 */
export function proofReferenceText(proofFileUrl) {
  return presentText(proofFileUrl)
}
