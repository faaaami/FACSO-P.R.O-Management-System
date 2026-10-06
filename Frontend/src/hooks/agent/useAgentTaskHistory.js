import { useQuery } from '@tanstack/react-query'
import { getAgentTaskHistory } from '../../api/agent/tasks'

/**
 * Loads the status-change history of one renewal task.
 * Backing query GET /api/v1/agent/tasks/{taskId}/history
 * Returns GetRenewalTaskHistoryResponseDto { taskId, items, totalCount }
 *   item: RenewalTaskHistoryDto { id, status, changedBy, changedAt, note? }
 */
export function useAgentTaskHistory(taskId) {
  return useQuery({
    queryKey: ['agent', 'task', taskId, 'history'],
    queryFn: () => getAgentTaskHistory(taskId),
    enabled: Boolean(taskId),
    staleTime: 30_000,
    retry: 1,
  })
}
