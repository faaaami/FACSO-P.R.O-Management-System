import apiClient from '../axios'

/**
 * Admin service-request endpoints. `[Authorize(Roles = "Admin")]` under
 * /api/v1/service-requests.
 *
 * KNOWN LIMITATION (drives the approved Service Requests IA): the list endpoint
 * accepts ONLY page and pageSize. There is no status, type, company, date or
 * text filter, so the Admin page is an unfiltered paged list.
 */

/**
 * GET /api/v1/service-requests
 * Query: page (1), pageSize (20)
 * Returns { items, page, pageSize, totalCount }
 *   item: ServiceRequestListItemDto { id, clientCompanyId, type, employeeId?,
 *     entityId?, documentId?, status, description?, rejectionReason?,
 *     convertedAt?, rejectedAt?, convertedRenewalTaskId?, createdAt }
 */
export async function getAdminServiceRequests(params = {}) {
  const response = await apiClient.get('/service-requests', { params })
  return response.data.data
}

/**
 * GET /api/v1/service-requests/{id}
 * 404 => NOT_FOUND (KeyNotFoundException is mapped to 404 by GlobalExceptionMiddleware).
 */
export async function getAdminServiceRequestById(id) {
  const response = await apiClient.get(`/service-requests/${id}`)
  return response.data.data
}

/**
 * PATCH /api/v1/service-requests/{id}/convert
 *
 * Takes NO request body. The command is ConvertServiceRequestCommand(Guid
 * ServiceRequestId), and the document the task is created from is read from the
 * request's own linked DocumentId — the route exposes no way to substitute a
 * different document, which is why the dialog cannot offer one.
 *
 * The document is resolved server-side from the request record, which is also why
 * this route can answer 409 "No document linked to this request." for a request
 * type that was created without one.
 *
 * 404 => NOT_FOUND, 409 => CONFLICT.
 * Returns ConvertServiceRequestResponseDto { serviceRequestId, renewalTaskId,
 *   status, convertedAt }
 */
export async function convertAdminServiceRequest(id) {
  const response = await apiClient.patch(`/service-requests/${id}/convert`)
  return response.data.data
}

/**
 * PATCH /api/v1/service-requests/{id}/reject
 * Body: RejectServiceRequestRequest { reason }
 * 404 => NOT_FOUND, 400 => VALIDATION, 409 => CONFLICT
 * Returns RejectServiceRequestResponseDto { serviceRequestId, status,
 *   rejectionReason, rejectedAt }
 *
 * `reason` is required and capped at 1000 characters server-side, and the handler
 * stores it trimmed. The wrapper trims as well so the length the Admin is shown
 * and the length the server validates are the same number; it does NOT reject a
 * blank reason locally, because the server owns that rule and duplicating it here
 * would give the caller two different error paths for one condition.
 */
export async function rejectAdminServiceRequest(id, reason) {
  const response = await apiClient.patch(`/service-requests/${id}/reject`, {
    reason: typeof reason === 'string' ? reason.trim() : reason,
  })
  return response.data.data
}
