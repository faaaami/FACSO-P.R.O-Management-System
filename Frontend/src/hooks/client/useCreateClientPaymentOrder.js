import { useMutation, useQueryClient } from '@tanstack/react-query'
import { createClientPaymentOrder } from '../../api/client/billing'

export function useCreateClientPaymentOrder() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: createClientPaymentOrder,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['client', 'payment-history'] })
      queryClient.invalidateQueries({ queryKey: ['client', 'retainer-invoices'] })
      queryClient.invalidateQueries({ queryKey: ['client', 'service-fee-invoices'] })
    },
  })
}