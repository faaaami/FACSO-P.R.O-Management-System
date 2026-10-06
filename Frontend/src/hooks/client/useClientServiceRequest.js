import { useEffect, useRef } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getClientServiceRequest } from '../../api/client/serviceRequests'

/**
 * How often an undecided request is re-read while its drawer is open.
 */
const DECISION_POLL_INTERVAL_MS = 5_000

/**
 * How long the automatic check runs before it stops and asks the client to try
 * again. Bounded on purpose.
 *
 * A decision is made by an admin in a different module, so a client watching a
 * request they have just submitted has no other way to learn the outcome short of
 * reloading by hand. Polling is therefore worth doing — but only for a short while.
 * An unbounded interval would keep a request open on a phone, quietly, for hours,
 * and would keep a network request alive per open drawer indefinitely. Five seconds
 * for two minutes is long enough to catch a decision taken straight away, which is
 * the realistic case, and then the drawer stops and offers a manual check.
 */
const DECISION_POLL_WINDOW_MS = 120_000

/**
 * One service request, company-scoped server-side.
 * Backing query GET /api/v1/client/service-requests/{id}
 *
 * POLLING WHILE THE REQUEST IS UNDECIDED, AND ONLY THEN. `refetchInterval` is
 * computed per poll from the last response rather than set once, so the two
 * conditions that end polling are both server facts:
 *
 *   status !== 'Submitted'  the request has been converted or rejected, so the
 *                           outcome is on screen and there is nothing left to wait
 *                           for. Polling stops.
 *   the window has elapsed  see DECISION_POLL_WINDOW_MS.
 *
 * A request that arrives already Converted or Rejected therefore never polls at
 * all, which is the common case on a drawer reopened days later.
 *
 * `refetchIntervalInBackground` is left at its default of false, so a backgrounded
 * tab stops polling entirely rather than continuing to fetch where the client is
 * not looking. That is the desired behaviour for this screen, not a limitation.
 *
 * `pollUntilDecided: false` disables the interval for a caller that only wants a
 * one-shot read.
 */
export function useClientServiceRequest(id, { pollUntilDecided = true } = {}) {
  // The window is a single deadline rather than a counter, so it cannot be reset
  // by a slow response or extended indefinitely by a refetch that lands late.
  const deadlineRef = useRef(null)

  // A different request is a different decision to wait for, so its window starts
  // over rather than inheriting whatever was left of the previous one's.
  useEffect(() => {
    deadlineRef.current = null
  }, [id])

  return useQuery({
    queryKey: ['client', 'service-request', id],
    queryFn: () => getClientServiceRequest(id),
    enabled: Boolean(id),
    staleTime: 30_000,
    refetchInterval: (query) => {
      if (!pollUntilDecided) {
        return false
      }

      const data = query.state.data

      // Decided: the outcome is already rendered, so stop.
      if (data && data.status !== 'Submitted') {
        return false
      }

      // No response yet, or still undecided: this is the state worth polling for.
      if (deadlineRef.current === null) {
        deadlineRef.current = Date.now() + DECISION_POLL_WINDOW_MS
      }

      if (Date.now() >= deadlineRef.current) {
        return false
      }

      return DECISION_POLL_INTERVAL_MS
    },
  })
}
