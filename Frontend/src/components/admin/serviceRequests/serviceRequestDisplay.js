import {
  SERVICE_REQUEST_STATUS,
  SERVICE_REQUEST_STATUS_VALUES,
  SERVICE_REQUEST_TYPES,
  serviceRequestStatusLabel,
  serviceRequestTypeLabel,
} from '../enumLabels'
import { MISSING_VALUE, formatDate, formatDateTime, presentText, truncate } from '../clients/clientDisplay'

/**
 * Presentation helpers for the Admin Service Requests module.
 *
 * Every field referenced anywhere in this module was read out of the backend
 * response records:
 *
 *   ServiceRequestListItemDto       id, clientCompanyId, type, employeeId?, entityId?,
 *                                   documentId?, status, description?, rejectionReason?,
 *                                   convertedAt?, rejectedAt?, convertedRenewalTaskId?,
 *                                   createdAt
 *   GetServiceRequestResponseDto    the above + updatedAt, and NOTHING else
 *
 * The detail DTO adds exactly one field over the list DTO. There is no company
 * name, no employee name, no entity name, no document number, no comment, no
 * attachment and no history on either of them: every entity reference is a bare
 * Guid. `useAdminServiceRequestSubjects` resolves what the Admin API can actually
 * resolve, and nothing in this file invents a value to fill a gap.
 *
 * ServiceRequestStatus is Submitted=1, Converted=2, Rejected=3 — a DIFFERENT enum
 * from RenewalTaskStatus. The Renewal Task tone map and status chain are not
 * imported, referenced or extended here, and a status the backend adds later
 * must not silently inherit a Renewal Task colour.
 *
 * Shared formatting primitives are imported from the completed Admin Clients
 * module and re-exported, following the same decision taskDisplay.js made, so
 * this module has ONE import site for date and missing-value formatting.
 *
 * Nothing in this file encodes workflow. Both terminal transitions are
 * irreversible, and the ONE predicate this module needs — that a request is still
 * `Submitted` and can therefore be decided — lives in the drawer, next to the
 * buttons it gates, rather than here. A broader restatement of the workflow would
 * be a second source of truth with nothing left to gate, and would drift from the
 * handlers that actually enforce it.
 */

/** Re-exported shared formatting primitives — see the note above. */
export { MISSING_VALUE, formatDate, formatDateTime, presentText, truncate }

/**
 * The three statuses in enum-ordinal order, derived from the verified ordinals
 * rather than written out by hand, so any future ordering uses the enum's own.
 */
export const SERVICE_REQUEST_STATUS_KEYS = Object.keys(SERVICE_REQUEST_STATUS_VALUES).sort(
  (a, b) => SERVICE_REQUEST_STATUS_VALUES[a] - SERVICE_REQUEST_STATUS_VALUES[b],
)

/**
 * APPROVED DESIGN.md TONE MAP — the single source of status colour in this module.
 *
 *   Submitted -> warning  (the open state; work is still outstanding)
 *   Converted -> success  (a renewal task now exists)
 *   Rejected  -> danger   (declined, with a recorded reason)
 *
 * These are the same three DESIGN.md tokens the Renewal Tasks module uses for its
 * own statuses, chosen independently for this enum: #D97706 warning, #0F9D74
 * success, #DC2626 danger. The map is LOCAL to Admin Service Requests.
 *
 * Colour is decorative only. Every status ships its text label through StatusPill
 * or an adjacent text node, so status is never communicated by colour alone.
 *
 * An UNRECOGNISED member falls through to 'warning' rather than to a healthy
 * tone: a status the backend adds later must stay visibly "needs attention"
 * instead of silently inheriting the Converted palette.
 */
export const SERVICE_REQUEST_STATUS_TONE = {
  Submitted: 'warning',
  Converted: 'success',
  Rejected: 'danger',
}

export function serviceRequestStatusTone(status) {
  if (status == null) return 'neutral'
  return SERVICE_REQUEST_STATUS_TONE[status] ?? 'warning'
}

/**
 * Human label for a status, delegated to the shared admin enumLabels helper so
 * there is exactly one place that knows the display wording. Not reimplemented.
 */
export function serviceRequestStatusText(status) {
  return serviceRequestStatusLabel(status) ?? null
}

/** Human label for a request type, delegated for the same reason. */
export function serviceRequestTypeText(type) {
  return serviceRequestTypeLabel(type) ?? null
}

/**
 * Shortens a Guid to an explicitly UNRESOLVED reference.
 *
 * Bare Guids reach this module from three places the Admin API cannot resolve:
 * an entity id on an entity-only request inside the list, an employee beyond the
 * 1,000-row lookup cap, and the converted renewal task id, which has no
 * addressable Admin route at all. None may be rendered as if it were a name, so
 * the caller labels the result as unresolved and the full value stays available
 * in the element's `title`.
 */
export function shortGuid(value) {
  const text = presentText(value)
  if (!text) return null
  return text.length > 8 ? `${text.slice(0, 8)}…` : text
}

/**
 * `description` is the free-text notes the client typed when submitting. It is
 * the only narrative field on either DTO, and it is frequently absent, so every
 * call site must handle null rather than assume prose.
 *
 * Returns a present string or null, so a caller can render the honest dash
 * instead of an empty paragraph.
 */
export function requestDescriptionText(description) {
  return presentText(description)
}

/** A single-line preview of the notes for a table cell. */
export function requestDescriptionPreview(description, max = 90) {
  const text = requestDescriptionText(description)
  if (!text) return null
  return truncate(text, max)
}

/**
 * Whether the request is about a person or an organisation, derived only from
 * which id the DTO actually carries.
 *
 * The backend's CK_service_requests_subject constraint guarantees at least one
 * of employeeId / entityId is present, and a request may carry BOTH (the create
 * handler accepts an employee plus that employee's entity). So this is a label
 * for the PRIMARY subject, not a claim that the other is absent — the subject
 * section renders each id independently and never uses this to hide one.
 */
export function subjectKindText(request) {
  if (request?.employeeId) return 'Employee'
  if (request?.entityId) return 'Entity'
  return null
}

/** True when the request has neither an employee nor an entity subject. */
export function hasSubject(request) {
  return Boolean(request?.employeeId || request?.entityId)
}

/**
 * The status values as plain text, for a description that must not drift from
 * the enum. Kept next to the tone map so the two are read together.
 */
export const SERVICE_REQUEST_STATUS_WORDS = SERVICE_REQUEST_STATUS

/** The request types as plain text, likewise. */
export const SERVICE_REQUEST_TYPE_WORDS = SERVICE_REQUEST_TYPES
