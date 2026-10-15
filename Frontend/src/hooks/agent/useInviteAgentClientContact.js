import { useMutation } from '@tanstack/react-query'
import { inviteAgentClientContact } from '../../api/agent/clients'

/** POST /api/v1/agent/clients/{clientId}/contacts (no Agent read shows contacts). */
export function useInviteAgentClientContact() {
  return useMutation({
    mutationFn: ({ clientId, ...payload }) => inviteAgentClientContact(clientId, payload),
  })
}
