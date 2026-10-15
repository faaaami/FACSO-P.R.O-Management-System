import apiClient from '../axios'

/**
 * Admin employee endpoints. `[Authorize(Roles = "Admin")]` under /api/v1/admin.
 *
 * KNOWN LIMITATION: the list endpoint accepts no text search, so the Admin
 * Employees page cannot offer server-side search.
 */

/**
 * GET /api/v1/admin/employees
 * Query: clientId (Guid?), entityId (Guid?), isActive (bool?), page (1), pageSize (20)
 * Returns { items, page, pageSize, totalCount }
 */
export async function getAdminEmployees(params = {}) {
  const response = await apiClient.get('/admin/employees', { params })
  return response.data.data
}

/**
 * GET /api/v1/admin/employees/{employeeId}
 * Query: includeDeleted (false)
 * 404 => EMPLOYEE_NOT_FOUND
 */
export async function getAdminEmployeeById(employeeId, params = {}) {
  const response = await apiClient.get(`/admin/employees/${employeeId}`, { params })
  return response.data.data
}

/**
 * GET /api/v1/admin/employees/{employeeId}/documents
 * Query: includeDeleted (false)
 * Returns { items, totalCount }
 *
 * CORRECTION (Admin Employees Phase 2): an earlier revision of this comment
 * claimed `page (1), pageSize (20)`. That was wrong. The controller action
 * EmployeesController.GetDocuments binds only `includeDeleted` — there is no page
 * or pageSize parameter and no page/pageSize in the response. The endpoint is
 * unpaginated, so the UI must not render a pager for it and must not treat
 * `totalCount` as a page count.
 *
 * The route 404s when the parent employee is missing OR soft-deleted, and that
 * gate is evaluated before `includeDeleted` is honoured, so a deleted employee's
 * documents cannot be read back even with includeDeleted=true.
 *
 * Employee documents are rows in the shared `documents` table keyed by
 * `employee_id` (exactly one of client_entity_id / employee_id is set), which is
 * why the shared `getAdminDocumentFile` in api/admin/documents.js streams a file
 * for them. That wrapper is reused rather than duplicated here.
 */
export async function getAdminEmployeeDocuments(employeeId, params = {}) {
  const response = await apiClient.get(`/admin/employees/${employeeId}/documents`, {
    params,
  })
  return response.data.data
}

/**
 * GET /api/v1/admin/employees/{employeeId}/timeline
 * Query: page (1), pageSize (20)
 * Returns { employeeId, items, page, pageSize, totalCount }
 *   item: EmployeeTimelineItemDto { id, action, entityType, entityId,
 *     description, createdAt }
 *
 * A genuinely paginated audit trail: this is the one employee child route that
 * returns page, pageSize and totalCount, so the pager here is backed by real
 * metadata. `action` and `entityType` are free-form strings written by the
 * command handlers, not an enum — see employeeDisplay.timelineActionLabel.
 */
export async function getAdminEmployeeTimeline(employeeId, params = {}) {
  const response = await apiClient.get(`/admin/employees/${employeeId}/timeline`, {
    params,
  })
  return response.data.data
}

/**
 * GET /api/v1/admin/employees/expiring
 * Query: days (30), page (1), pageSize (20), includeExpired (false)
 * Returns { page, pageSize, totalCount, items }
 *   item: ExpiringEmployeeDocumentDto { employeeId, fullName, clientEntityId,
 *     documentId, documentNumber, expiryDate, daysRemaining, status }
 *
 * A GENUINELY PAGINATED list, unlike the per-employee documents route above, so
 * the pager for it is backed by real metadata.
 *
 * WHY THIS ROUTE AND NOT /admin/dashboard/expiry-alerts/employees. Both answer
 * "which documents are expiring", but the dashboard variant returns the document
 * alert DTO, which has no employee identity at all — a row cannot say whose
 * document it is, so on a multi-employee client it is not actionable. This route
 * carries employeeId, fullName and clientEntityId on every row and is the source
 * for the Employees page.
 *
 * `days` is validated by the backend and 400s outside 1..365, so the wrapper
 * forwards it as given and the hook clamps before it gets here.
 *
 * `daysRemaining` is the backend's own arithmetic. It is signed — positive
 * counts days until expiry, negative days since — and floored, so 0 means "less
 * than a day left" rather than "today". Callers must not recompute it from
 * `expiryDate`, because a second calculation can disagree by a day at the
 * boundary and would then contradict the status printed beside it.
 *
 * `status` is a plain string written by the repository, not the DocumentStatus
 * enum used by the stored-document endpoints; the two vocabularies must not be
 * reconciled in the browser.
 */
export async function getAdminExpiringEmployees(params = {}) {
  const response = await apiClient.get('/admin/employees/expiring', { params })
  return response.data.data
}
