import { useQuery } from '@tanstack/react-query'
import { getRetainerInvoice } from '../../api/client/billing'

/**
 * @param {string} id Retainer invoice id.
 * @param {object} [options]
 * @param {boolean} [options.poll] Poll while a payment is being confirmed.
 *   Off by default: the detail query is otherwise only fetched on open, and
 *   polling it would keep hitting the API for every drawer the client opens.
 *   The invoice detail carries the linked payment order's status
 *   (`paymentOrderStatus`), which is what the checkout waits on.
 */
export function useRetainerInvoice(id, { poll = false } = {}) {
  return useQuery({
    queryKey: ['client', 'retainer-invoice', id],
    queryFn: () => getRetainerInvoice(id),
    enabled: Boolean(id),
    staleTime: 30_000,
    // Poll only while confirming. A settled invoice stops polling because the
    // drawer turns the flag off, so this never runs indefinitely.
    refetchInterval: poll ? 3_000 : false,
  })
}
