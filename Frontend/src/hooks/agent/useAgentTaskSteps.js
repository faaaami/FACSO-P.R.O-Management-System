import { useQuery } from '@tanstack/react-query'
import { getAgentTaskSteps } from '../../api/agent/tasks'

/**
 * Loads the renewal-step log of one renewal task.
 * Backing query GET /api/v1/agent/tasks/{taskId}/steps
 * Returns GetRenewalTaskStepsResponseDto { taskId, items, totalCount }
 *   item: RenewalStepLogDto { id, stepName, completedBy, completedAt,
 *     referenceNumber?, proofFileUrl? }
 */
export function useAgentTaskSteps(taskId) {
  return useQuery({
    queryKey: ['agent', 'task', taskId, 'steps'],
    queryFn: () => getAgentTaskSteps(taskId),
    enabled: Boolean(taskId),
    staleTime: 30_000,
    retry: 1,
  })
}
