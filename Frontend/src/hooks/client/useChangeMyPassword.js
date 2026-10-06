import { useMutation } from '@tanstack/react-query'
import { changeMyPassword } from '../../api/client/account'

export function useChangeMyPassword() {
  return useMutation({
    mutationFn: changeMyPassword,
  })
}