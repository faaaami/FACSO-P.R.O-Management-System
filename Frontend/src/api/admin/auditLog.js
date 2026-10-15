import apiClient from '../axios'

/**
 * Admin audit-log endpoint. `[Authorize(Roles = "Admin")]` under /api/v1.
 *
 * KNOWN LIMITATIONS: no free-text search, no action filter, no sort control, no
 * single-entry detail, no export, and no IP address or user-agent columns. The
 * only filters are exact entityType / entityId / userId and a date range, so the
 * Audit Log page must present them as exact-match controls.
 */

/**
 * GET /api/v1/audit-log
 * Query: entityType (string?), entityId (Guid?), userId (Guid?), from (DateTime?),
 *   to (DateTime?), page (1), pageSize (20)
 * item carries: id, userId, userName, action, entityType, entityId, employeeId,
 *   description, metadata, createdAt
 *
 * All of userId, userName, employeeId, description and metadata are nullable;
 * entityId and createdAt are not.
 */
export async function getAdminAuditLog(params = {}) {
  const response = await apiClient.get('/audit-log', { params })
  return response.data.data
}
