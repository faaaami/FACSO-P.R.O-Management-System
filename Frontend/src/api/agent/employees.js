import apiClient from '../axios'

/**
 * Retrieves the paginated list of employees visible to the current Agent.
 * GET /api/v1/agent/employees
 * Query: { page = 1, pageSize = 20 }
 * The endpoint accepts no filter parameters. Employees are task-scoped: an
 * employee is returned only when they hold a document that is the document of
 * a non-deleted renewal task assigned to the signed-in Agent.
 * Returns GetEmployeesResponseDto { items, page, pageSize, totalCount }
 *   item: GetEmployeeListItemDto { id, clientEntityId, entityName, fullName,
 *     passportNumber?, nationality?, jobTitle?, hireDate, isActive, isDeleted,
 *     createdAt }
 */
export async function getAgentEmployees(params = {}) {
  const response = await apiClient.get('/agent/employees', { params })
  return response.data.data
}

/**
 * Retrieves a single employee in the Agent's task-scoped portfolio.
 * GET /api/v1/agent/employees/{id}
 * Returns GetEmployeeByIdResponseDto { id, clientEntityId, clientCompanyId,
 *   entityName, fullName, passportNumber?, dateOfBirth?, nationality?, jobTitle?,
 *   hireDate, isActive, isDeleted, createdAt, updatedAt }
 */
export async function getAgentEmployeeById(id) {
  const response = await apiClient.get(`/agent/employees/${id}`)
  return response.data.data
}

/**
 * Retrieves the documents held by a single employee in the Agent's portfolio.
 * GET /api/v1/agent/employees/{id}/documents
 * Returns GetEmployeeDocumentsResponseDto { items, totalCount }
 *   item: EmployeeDocumentListItemDto { id, employeeId, type, documentNumber,
 *     issueDate?, expiryDate?, fileUrl?, fileName?, contentType?, fileSize?,
 *     isActive, isDeleted, createdAt, updatedAt, status }
 */
export async function getAgentEmployeeDocuments(id) {
  const response = await apiClient.get(`/agent/employees/${id}/documents`)
  return response.data.data
}
