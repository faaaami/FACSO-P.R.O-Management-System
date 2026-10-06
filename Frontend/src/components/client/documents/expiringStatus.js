import { enumLabel } from '../enumLabels'

/**
 * Label map for the expiring-documents endpoint's OWN status vocabulary.
 *
 * This is deliberately NOT the stored `DocumentStatus` enum exported as
 * `DOCUMENT_STATUS` from `../enumLabels`, which is a different contract with
 * different members: it contains `Overdue` and `InRenewal` and has no
 * `Expired` at all. Conflating the two is what previously produced a raw
 * `ExpiringSoon` enum-string leak on the panel and a `daysRemaining <= 0`
 * status guess on the dashboard. `Overdue` remains a legitimate value for the
 * stored document list/detail statuses elsewhere; it simply does not exist on
 * this endpoint.
 *
 * The backend supplies these values itself via
 * `SqlSnippets.ExpiryStatusLabelCase(..., 90)` inside
 * `DocumentExpiryReadRepository`, so the UI only labels and tones them.
 */
export const EXPIRING_DOCUMENT_STATUS = {
  Active: 'Active',
  ExpiringSoon: 'Expiring Soon',
  Expired: 'Expired',
}

/**
 * Human label for the server-computed expiring status. Unknown values fall
 * through `enumLabel`'s own `?? String(value)` guard, so this never returns
 * null or undefined and needs no additional fallback.
 */
export function expiringStatusLabel(status) {
  return enumLabel(EXPIRING_DOCUMENT_STATUS, status)
}

/**
 * Maps the server-computed expiring status to a `StatusPill` tone.
 *
 * `status` is the sole authority for expiry state. `daysRemaining` must never
 * drive this: the backend computes it in SQL as
 * `FLOOR(EXTRACT(EPOCH FROM (expiry_date - CURRENT_TIMESTAMP)) / 86400)`, so it
 * is SIGNED and FLOORED. A document expiring in a few hours therefore reports
 * `0` while still being `ExpiringSoon`; inferring `danger` from a non-positive
 * number would wrongly paint a not-yet-expired document as expired.
 */
export function expiringStatusTone(status) {
  switch (status) {
    case 'Expired':
      return 'danger'
    case 'ExpiringSoon':
      return 'warning'
    case 'Active':
      return 'success'
    default:
      return 'neutral'
  }
}
