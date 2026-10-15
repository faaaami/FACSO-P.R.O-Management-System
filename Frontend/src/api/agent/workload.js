import apiClient from '../axios'

/**
 * Retrieves the current Agent's workload summary.
 * GET /api/v1/agent/workload
 * Returns GetStaffWorkloadResponseDto { staffId, activeTaskCount,
 *   blockedTaskCount, tasksByStatus: {Submitted,FeePaid,AwaitingApproval,Blocked,Approved,Updated} }
 */
export async function getAgentWorkload() {
  const response = await apiClient.get('/agent/workload')
  return response.data.data
}
