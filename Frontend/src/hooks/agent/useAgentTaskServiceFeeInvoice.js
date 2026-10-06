import { useQuery } from '@tanstack/react-query'
import { getAgentTaskServiceFeeInvoice } from '../../api/agent/tasks'

export function useAgentTaskServiceFeeInvoice(taskId, options = {}) {
  const enabled = options.enabled ?? false

  return useQuery({
    queryKey: ['agent', 'task', taskId, 'service-fee-invoice'],
    queryFn: () => getAgentTaskServiceFeeInvoice(taskId),
    enabled: Boolean(taskId) && enabled,
    staleTime: 30_000,
    retry: false,
  })
}
