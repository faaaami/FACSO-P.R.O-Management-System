import { useMutation } from '@tanstack/react-query'
import { changeAgentPassword } from '../../api/agent/me'

export function useChangeAgentPassword() {
  return useMutation({
    mutationFn: changeAgentPassword,
  })
}
