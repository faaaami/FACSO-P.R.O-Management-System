import apiClient from '../axios'

/**
 * Admin staff endpoints. `[Authorize(Roles = "Admin")]` under /api/v1/admin/staff.
 *
 * AGENT-ONLY DATASET — the single most important fact about this module. Every
 * handler in Staff/Queries/GetStaff, GetStaffById and the Update/Deactivate
 * commands filters `x.Role == UserRole.Agent`. There is therefore no role filter
 * in this UI, no role selector on the create form, and no way to manage an Admin
 * account from here: a non-Agent id 404s on every route below.
 *
 * KNOWN LIMITATION: the list endpoint accepts no text search, no role filter and
 * no sort parameter. Ordering is hard-coded `ORDER BY created_at DESC, id ASC`.
 * The Staff page's name/email box is therefore CLIENT-SIDE and scoped to the
 * currently loaded page only. This is a backend gap, not a UI omission.
 *
 * VERIFIED PAGINATION SHAPE: { items, totalCount } — no page/pageSize echoed, so
 * the caller keeps the page numbers it requested.
 *
 * StaffListItemDto.role is UserRole (string-serialized).
 *
 * VERIFIED AGAINST CreateStaffCommandHandler: an account created here is
 * immediately usable. Verified against source:
 *
 *   - The admin CHOOSES the password. CreateStaffRequestDto takes FullName,
 *     Email, Password, Phone. Nothing is generated server-side and no password is
 *     ever returned, so this UI must never display or echo one.
 *   - The handler hard-codes Role = Agent, IsActive = true, IsApproved = true and
 *     EmailVerified = true. None of the four is in the request DTO at all, so no
 *     control for any of them is offered here.
 *   - The account can therefore sign in straight away, with the password the
 *     admin just chose. There is no pending-approval state and no
 *     email-verification step to wait for, and neither is invented in the UI.
 *     This corrects an earlier version of this comment, which claimed the handler
 *     set EmailVerified = false and that the account was permanently unable to
 *     sign in; that is no longer what the handler does.
 *   - The weight that carries is the admin's. The password is chosen here and is
 *     never returned by any endpoint, so it cannot be handed to the new Agent out
 *     of band from inside this application, and it is not recoverable later. The
 *     dialog says exactly that instead of implying the system will.
 *
 * DEACTIVATION IS ONE-WAY. There is no reactivation endpoint: the module exposes
 * only Create, Update and Deactivate, Update does not write IsActive, and
 * Deactivate sets IsActive = false without soft-deleting the row. Nothing in the
 * UI may offer a restore.
 *
 * PARTIAL-UPDATE SEMANTICS FOR email AND isApproved. UpdateStaffCommandHandler
 * writes `email` and `isApproved` ONLY when the caller supplies them: both are
 * nullable on the DTO, and an omitted value is left untouched. Before this,
 * `isApproved` was a non-nullable bool that the handler assigned
 * unconditionally, so omitting it wrote `false` and silently revoked an approved
 * account (which in turn blocks login, because LoginCommandHandler refuses an
 * unapproved account) on an otherwise unrelated edit.
 *
 * `fullName` is still required: it is trimmed without a null guard, so omitting
 * it is a 400 from model validation, not a 500. `phone` is still written
 * unconditionally, so an omitted `phone` CLEARS the field rather than
 * preserving it — send `null` explicitly to clear and a value to set.
 *
 * The wrapper below sends exactly the four fields the DTO declares, and
 * AdminStaffFormDialog seeds all four from the current server row, so the
 * frontend behaviour is unchanged by the fix.
 */

/** GET /api/v1/admin/staff — Query: includeInactive (false), page (1), pageSize (20) */
export async function getAdminStaff(params = {}) {
  const response = await apiClient.get('/admin/staff', { params })
  return response.data.data
}

/** GET /api/v1/admin/staff/{staffId} — 404 => STAFF_NOT_FOUND */
export async function getAdminStaffById(staffId) {
  const response = await apiClient.get(`/admin/staff/${staffId}`)
  return response.data.data
}

/**
 * POST /api/v1/admin/staff
 * Body: CreateStaffRequestDto { fullName, email, password, phone? }
 *
 * There is deliberately no `role` and no `isApproved` in this payload: neither
 * exists on the DTO, and the handler decides both. `phone` is the only optional
 * field, so it is omitted when blank rather than sent as an empty string.
 */
export async function createAdminStaff(payload) {
  const body = {
    fullName: payload.fullName,
    email: payload.email,
    password: payload.password,
  }
  if (payload.phone) {
    body.phone = payload.phone
  }

  const response = await apiClient.post('/admin/staff', body)
  return response.data.data
}

/**
 * PATCH /api/v1/admin/staff/{staffId}
 * Body: UpdateStaffRequestDto { fullName, email, phone?, isApproved }
 *
 * Sent as a complete four-field object on every call. `email` and `isApproved`
 * are now nullable server-side and are only written when supplied, but this
 * wrapper always sends both, so an edit always carries the admin's current
 * intent. `phone` is always sent too (possibly null, which is how the field is
 * cleared).
 */
export async function updateAdminStaff(staffId, payload) {
  const response = await apiClient.patch(`/admin/staff/${staffId}`, {
    fullName: payload.fullName,
    email: payload.email,
    phone: payload.phone ?? null,
    isApproved: payload.isApproved,
  })
  return response.data.data
}

/**
 * DELETE /api/v1/admin/staff/{staffId} — NO REQUEST BODY.
 *
 * The controller takes only a route id, so this sends no second argument to
 * `delete`. Passing `{}` would be harmless but misleading about the contract.
 * Sets IsActive = false; does not soft-delete. 400 when already inactive, 404 for
 * an unknown or non-Agent id.
 */
export async function deactivateAdminStaff(staffId) {
  const response = await apiClient.delete(`/admin/staff/${staffId}`)
  return response.data.data
}

/**
 * GET /api/v1/admin/staff/workload
 * Returns { items: [{ staffId, activeTaskCount, blockedTaskCount, tasksByStatus }] }
 *   tasksByStatus always carries all six keys: Submitted, FeePaid,
 *   AwaitingApproval, Blocked, Approved, Updated.
 *
 * VERIFIED CORRECT — an earlier note here claimed a bug, and that note was wrong.
 * This endpoint is NOT the broken /dashboard/staff-workload endpoint. Its SQL
 * labels Blocked as status 4 (correct, not 6), uses a LEFT JOIN so agents with
 * zero tasks still appear, and scopes to assigned tasks on users with the Agent
 * role. It is the dashboard's single workload source.
 *
 * ONE SCOPE CAVEAT, not a bug: because it aggregates by assigned staff, an
 * UNASSIGNED task appears in no row. The dashboard labels these figures
 * "assigned" for that reason.
 */
export async function getAdminStaffWorkload() {
  const response = await apiClient.get('/admin/staff/workload')
  return response.data.data
}

/** GET /api/v1/admin/staff/{staffId}/workload — 404 => STAFF_NOT_FOUND */
export async function getAdminStaffWorkloadById(staffId) {
  const response = await apiClient.get(`/admin/staff/${staffId}/workload`)
  return response.data.data
}
