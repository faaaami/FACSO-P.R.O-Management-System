import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  updateAgentTaskStatus,
  blockAgentTask,
  addAgentTaskStep,
} from '../../api/agent/tasks'

export function useAgentTaskActions() {
  const queryClient = useQueryClient()

  function invalidateTask(taskId) {
    queryClient.invalidateQueries({ queryKey: ['agent', 'tasks'] })
    queryClient.invalidateQueries({ queryKey: ['agent', 'workload'] })
    // Prefix match under TanStack Query's default partial matching, so this
    // single call also covers ['agent','task',id,'history'], ['steps'] and
    // ['service-fee-invoice'].
    queryClient.invalidateQueries({ queryKey: ['agent', 'task', taskId] })
  }

  const updateStatus = useMutation({
    mutationFn: ({ taskId, status, note, renewedExpiryDate }) =>
      updateAgentTaskStatus(taskId, { status, note, renewedExpiryDate }),
    onSuccess: (_data, variables) => {
      invalidateTask(variables.taskId)
      // A status change raises a notification for the assigned Agent, so the
      // notification lists and their unread counts must be refetched.
      queryClient.invalidateQueries({ queryKey: ['agent', 'notifications'] })

      // Completing a task also rewrites the linked document's expiry, so the
      // document views that display it are stale. Gated on the date actually
      // being sent: a non-completing transition changes no document, and only
      // an Agent session can hold these keys, so the admin/client document
      // caches are deliberately left alone.
      if (variables.renewedExpiryDate) {
        if (variables.documentId) {
          queryClient.invalidateQueries({
            queryKey: ['agent', 'document', variables.documentId],
          })
        }
        queryClient.invalidateQueries({ queryKey: ['agent', 'documents'] })
      }
    },
  })

  const block = useMutation({
    mutationFn: ({ taskId, blockedReason }) => blockAgentTask(taskId, { blockedReason }),
    onSuccess: (_data, variables) => {
      invalidateTask(variables.taskId)
    },
  })

  const addStep = useMutation({
    mutationFn: ({ taskId, ...payload }) => addAgentTaskStep(taskId, payload),
    onSuccess: (_data, variables) => {
      invalidateTask(variables.taskId)
    },
  })

  return {
    updateStatus,
    block,
    addStep,
  }
}
