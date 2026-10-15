import apiClient from '../axios'

/**
 * Retrieves the paginated list of renewal tasks assigned to the current Agent.
 * GET /api/v1/agent/tasks
 * Query: page (1), pageSize (20), status (RenewalTaskStatus?)
 * Returns GetRenewalTasksResponseDto { items, totalCount }
 *   item: RenewalTaskListItemDto { id, documentId, clientCompanyId,
 *     assignedStaffId?, status, blockedReason?, blockedSince?, completedAt?, createdAt }
 */
export async function getAgentTasks(params = {}) {
  const response = await apiClient.get('/agent/tasks', { params })
  return response.data.data
}

/**
 * Retrieves a single renewal task assigned to the current Agent.
 * GET /api/v1/agent/tasks/{taskId}
 */
export async function getAgentTaskById(taskId) {
  const response = await apiClient.get(`/agent/tasks/${taskId}`)
  return response.data.data
}

/**
 * Retrieves the status-change history of one assigned renewal task.
 * GET /api/v1/agent/tasks/{taskId}/history
 * Returns GetRenewalTaskHistoryResponseDto { taskId, items, totalCount }
 *   item: RenewalTaskHistoryDto { id, status, changedBy, changedAt, note? }
 */
export async function getAgentTaskHistory(taskId) {
  const response = await apiClient.get(`/agent/tasks/${taskId}/history`)
  return response.data.data
}

/**
 * Retrieves the renewal-step log of one assigned renewal task.
 * GET /api/v1/agent/tasks/{taskId}/steps
 * Returns GetRenewalTaskStepsResponseDto { taskId, items, totalCount }
 *   item: RenewalStepLogDto { id, stepName, completedBy, completedAt,
 *     referenceNumber?, proofFileUrl? }
 */
export async function getAgentTaskSteps(taskId) {
  const response = await apiClient.get(`/agent/tasks/${taskId}/steps`)
  return response.data.data
}

/**
 * Changes a renewal task's status.
 * PATCH /api/v1/agent/tasks/{taskId}/status
 *
 * `renewedExpiryDate` is REQUIRED when status is 'Updated' and ignored
 * otherwise. A completion rewrites the expiry of the document linked to the
 * task, so it is sent as a date-only 'YYYY-MM-DD' string; the server stores
 * that date at midnight UTC rather than the moment of the request.
 */
export async function updateAgentTaskStatus(
  taskId,
  { status, note, renewedExpiryDate } = {},
) {
  const payload = { status }
  if (note) {
    payload.note = note
  }
  if (renewedExpiryDate) {
    payload.renewedExpiryDate = renewedExpiryDate
  }
  const response = await apiClient.patch(`/agent/tasks/${taskId}/status`, payload)
  return response.data.data
}

export async function blockAgentTask(taskId, { blockedReason } = {}) {
  const response = await apiClient.patch(`/agent/tasks/${taskId}/block`, { blockedReason })
  return response.data.data
}

export async function addAgentTaskStep(taskId, payload = {}) {
  const body = { stepName: payload.stepName }
  if (payload.referenceNumber) {
    body.referenceNumber = payload.referenceNumber
  }
  if (payload.proofFileUrl) {
    body.proofFileUrl = payload.proofFileUrl
  }
  const response = await apiClient.post(`/agent/tasks/${taskId}/steps`, body)
  return response.data.data
}

export async function getAgentTaskServiceFeeInvoice(taskId) {
  const response = await apiClient.get(`/agent/tasks/${taskId}/service-fee-invoice`)
  return response.data.data
}
