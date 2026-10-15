import { USER_ROLES, enumLabel, RENEWAL_TASK_STATUS } from '../enumLabels'
import { MISSING_VALUE, presentText } from '../clients/clientDisplay'
import { RENEWAL_TASK_STATUS_KEYS } from '../../../hooks/admin/useAdminDashboard'

/**
 * Presentation helpers for the Admin Staff module.
 *
 * Field names were read out of the backend response records, not assumed:
 *
 *   StaffListItemDto         id, fullName, email, phone, role, isActive,
 *                            isApproved, emailVerified, createdAt
 *   GetStaffByIdResponseDto  the above + updatedAt
 *   GetStaffWorkload…Dto     staffId, activeTaskCount, blockedTaskCount,
 *                            tasksByStatus
 *
 * THE THREE STATUS BOOLEANS ARE DELIBERATELY NOT COLLAPSED.
 *
 * isActive, isApproved and emailVerified are independent columns, and each one
 * fails sign-in at a different point, so folding them into a single "status"
 * would hide which rule a person is actually tripping over:
 *
 *   isActive = false        the account is deactivated; the list hides it unless
 *                           "include inactive" is on. Not a login rule at all.
 *   isApproved = false      LoginCommandHandler throws ForbiddenAccessException
 *                           "Your account has not been approved yet."
 *   emailVerified = false   LoginCommandHandler throws ForbiddenAccessException
 *                           "Please verify your email before logging in."
 *
 * An Admin-created account arrives with ALL THREE flags already satisfied:
 * CreateStaffCommandHandler hard-codes IsActive = true, IsApproved = true AND
 * EmailVerified = true. So for the Admin-provisioned dataset none of the three
 * is the reason a person cannot sign in, and a single blended "Inactive /
 * Pending" pill would report the wrong reason by defaulting to a state that
 * cannot occur. The table therefore renders these as separate,
 * individually-labelled facts. Each one is still reachable — an Admin can
 * deactivate or revoke approval, and an Agent can arrive unverified through the
 * public Register flow — so none of the labels can be hard-coded either.
 *
 * STATUS TONES ARE DECORATIVE. Every pill ships with its text label, so none of
 * this relies on colour alone; the tones match the existing Admin pill scale
 * (success / warning / neutral / danger) rather than introducing new colours.
 */

/** Fallback when a record has no usable fullName. */
export function staffName(staff) {
  return presentText(staff?.fullName) ?? 'Unnamed staff member'
}

/** Human label for the UserRole enum, falling back to the raw value. */
export function staffRoleLabel(staff) {
  return enumLabel(USER_ROLES, staff?.role) ?? MISSING_VALUE
}

/**
 * The dataset is Agent-only: every staff handler filters `role = Agent`. Role is
 * still rendered from the payload rather than hard-coded, so if the backend ever
 * widens the filter the table starts telling the truth without a code change —
 * but nothing in this module offers a way to CHANGE it, because no endpoint
 * accepts a role.
 */
export function isAgentRole(staff) {
  return staff?.role === 'Agent'
}

/**
 * Account activity. This is the only one of the three booleans that is about the
 * account existing rather than about sign-in being permitted, so it is reported
 * on its own terms.
 */
export function staffActiveLabel(staff) {
  return staff?.isActive ? 'Active' : 'Inactive'
}

export function staffActiveTone(staff) {
  return staff?.isActive ? 'success' : 'neutral'
}

/** Approval gate. False blocks sign-in with "not been approved yet". */
export function staffApprovedLabel(staff) {
  return staff?.isApproved ? 'Approved' : 'Not approved'
}

export function staffApprovedTone(staff) {
  return staff?.isApproved ? 'success' : 'warning'
}

/**
 * Email verification. The flag still gates sign-in — LoginCommandHandler rejects
 * on !EmailVerified — but it is NOT a gate an Admin-created account is stuck
 * behind, and this is worth stating precisely because it used to be worded the
 * other way round.
 *
 * An Agent created through POST /admin/staff is created verified: the handler
 * hard-codes EmailVerified = true alongside IsActive = true and IsApproved =
 * true, and mints no token because none is needed. So the "not verified" branch
 * below is only reachable for an Agent that arrived through the public Register
 * flow, where the flag is genuinely false and genuinely blocks login.
 *
 * The label says "Cannot sign in" rather than merely "Not verified" because that
 * is the consequence for those rows, and a bare "Not verified" reads like a
 * pending state that something will resolve on its own.
 */
export function staffEmailVerifiedLabel(staff) {
  return staff?.emailVerified ? 'Email verified' : 'Email not verified — cannot sign in'
}

export function staffEmailVerifiedTone(staff) {
  return staff?.emailVerified ? 'success' : 'warning'
}

/**
 * Coerces a count that may arrive as a number, a numeric string or nothing.
 * Returns 0 for unusable input so an arithmetic total can never produce NaN in
 * the UI. This mirrors the dashboard's own toCount, which is module-private.
 */
export function toCount(value) {
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : 0
  }
  if (typeof value === 'string' && value.trim()) {
    const parsed = Number(value)
    return Number.isFinite(parsed) ? parsed : 0
  }
  return 0
}

/** The six renewal-task statuses, in the verified ordinal order the API uses. */
export const STAFF_WORKLOAD_STATUS_KEYS = RENEWAL_TASK_STATUS_KEYS

/** Human label for one tasksByStatus key, e.g. AwaitingApproval -> Awaiting Approval. */
export function workloadStatusLabel(key) {
  return enumLabel(RENEWAL_TASK_STATUS, key) ?? String(key)
}

/**
 * The six-way split, always all six keys in a fixed order.
 *
 * The backend populates every key, but a missing one is still rendered as 0
 * rather than dropped, so the row count never depends on the response. The total
 * is the SUM OF THESE SIX FIGURES and nothing else.
 *
 * It is deliberately not derived from activeTaskCount: that field repeats the
 * backend's 1,2,3,5 definition, which is a different set from all six statuses,
 * so mixing the two would produce a total that contradicts the rows above it.
 * The dashboard makes the same choice for the same reason.
 */
export function staffWorkloadRows(workload) {
  const byStatus = workload?.tasksByStatus
  return STAFF_WORKLOAD_STATUS_KEYS.map((key) => ({
    key,
    label: workloadStatusLabel(key),
    count: toCount(byStatus?.[key]),
  }))
}

/** Sum of the six status rows — the only total this module claims. */
export function staffWorkloadTotal(workload) {
  return staffWorkloadRows(workload).reduce((sum, row) => sum + row.count, 0)
}

/**
 * The count the deactivation safety gate is decided on: activeTaskCount +
 * blockedTaskCount, exactly as the approved rule states.
 *
 * Returned as null — never 0 — when the workload has not loaded or has failed.
 * The gate must be able to tell "no open work" apart from "we do not know", and
 * collapsing the second into the first would let an unverified deactivation
 * through on a failed request.
 */
export function openTaskCountForDeactivation(workload) {
  if (!workload) return null
  if (workload.activeTaskCount == null && workload.blockedTaskCount == null) return null
  return toCount(workload.activeTaskCount) + toCount(workload.blockedTaskCount)
}
