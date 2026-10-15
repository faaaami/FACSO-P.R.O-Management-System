import { useQuery } from '@tanstack/react-query'
import { getAgentTaskById } from '../../api/agent/tasks'

/**
 * Loads a single renewal task assigned to the current Agent.
 * Backing query GET /api/v1/agent/tasks/{id}
 * Returns GetRenewalTaskByIdResponseDto { id, status, documentId, document,
 *   clientCompanyId, assignedStaff?, blockedReason?, blockedSince?,
 *   completedAt?, stepLogCount, createdAt, updatedAt }
 */
export function useAgentTaskById(taskId) {
  return useQuery({
    queryKey: ['agent', 'task', taskId],
    queryFn: () => getAgentTaskById(taskId),
    enabled: Boolean(taskId),
    staleTime: 30_000,
    retry: 1,
  })
}
