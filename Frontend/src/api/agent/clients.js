import apiClient from '../axios'

/**
 * Retrieves the paginated list of client companies visible to the current Agent.
 * GET /api/v1/agent/clients
 * Query: page (1), pageSize (20), search (string?)
 * `search` matches company name or trade licence number only.
 * Returns GetClientCompaniesResponseDto { items, page, pageSize, totalCount }
 *   item: ClientCompanyListItemDto { id, companyName, tradeLicenseNumber?,
 *     phone?, email?, emirate?, isActive, isDeleted, createdAt }
 */
export async function getAgentClients(params = {}) {
  const response = await apiClient.get('/agent/clients', { params })
  return response.data.data
}

/**
 * Retrieves a single client company visible to the current Agent.
 * GET /api/v1/agent/clients/{id}
 *
 * Agent visibility is task-mediated: the company is returned only when at
 * least one non-deleted renewal task for it is assigned to the caller. This is
 * not a permanent agent-to-company assignment.
 *
 * Returns GetClientCompanyByIdResponseDto { id, companyName,
 *   tradeLicenseNumber?, phone?, email?, address?, emirate?, isActive,
 *   isDeleted, createdAt, updatedAt }
 *
 * Note this DTO is richer than the list DTO (it adds `address` and
 * `updatedAt`), so the detail view must call this endpoint rather than
 * reusing list data.
 *
 * 404 => error code CLIENT_COMPANY_NOT_FOUND (also returned for companies
 * outside the caller's task scope, so a 404 never confirms existence).
 */
export async function getAgentClientById(clientId) {
  const response = await apiClient.get(`/agent/clients/${clientId}`)
  return response.data.data
}

/**
 * Retrieves the legal entities of a client company that are visible to the
 * current Agent.
 * GET /api/v1/agent/clients/{id}/entities
 * Query: page (1), pageSize (20), search (string?)
 * `search` matches entity name or trade licence number only.
 *
 * Scope note: this is narrower than the by-id client query. A row is only
 * returned when the caller's assigned task references a document linked to
 * that entity (directly via documents.client_entity_id, or via
 * documents.employee_id -> employees.client_entity_id). A company that
 * resolves at /agent/clients/{id} can therefore legitimately return zero
 * entities here.
 *
 * Returns GetClientEntitiesResponseDto { items, page, pageSize, totalCount }
 *   item: GetClientEntityListItemDto { id, clientCompanyId, entityName,
 *     tradeLicenseNumber?, emirate?, isActive, isDeleted, createdAt }
 *
 * 404 => generic NOT_FOUND (out-of-scope or non-existent parent).
 */
export async function getAgentClientEntities(clientId, params = {}) {
  const response = await apiClient.get(`/agent/clients/${clientId}/entities`, {
    params,
  })
  return response.data.data
}
