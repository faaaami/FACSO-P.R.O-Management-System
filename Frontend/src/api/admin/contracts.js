import apiClient from '../axios'

/**
 * Admin service-contract endpoints. `[Authorize(Roles = "Admin")]` under /api/v1.
 *
 * Contracts are always scoped to a client company (approved decision Q8: they
 * live inside the Admin Client detail drawer, not on their own page).
 *
 * ServiceContractStatus is Active=1, Ended=2, string-serialized.
 *
 * KNOWN LIMITATION: there is NO terminate/cancel route. PATCH /contracts/{id}
 * updates only StartDate, EndDate, RetainerAmount and Terms — it never touches
 * Status, and nothing else changes it. Do not render a Terminate action.
 *
 * CAVEAT: GetActive filters on Status == Active only; it does not compare dates,
 * so a contract past its EndDate still comes back as active.
 */

/** GET /api/v1/clients/{clientCompanyId}/contracts — Query: page (1), pageSize (20) */
export async function getAdminClientContracts(clientCompanyId, params = {}) {
  const response = await apiClient.get(`/clients/${clientCompanyId}/contracts`, {
    params,
  })
  return response.data.data
}

/**
 * GET /api/v1/clients/{clientCompanyId}/contracts/active
 * Returns the DTO directly, or null when the company has no active contract.
 */
export async function getAdminActiveContract(clientCompanyId) {
  const response = await apiClient.get(`/clients/${clientCompanyId}/contracts/active`)
  return response.data.data
}

/** GET /api/v1/contracts/{id} */
export async function getAdminContractById(contractId) {
  const response = await apiClient.get(`/contracts/${contractId}`)
  return response.data.data
}
