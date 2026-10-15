/**
 * Audit Log display helpers and filter primitives.
 *
 * ENTITY_TYPE_OPTIONS IS THE VERIFIED BACKEND VOCABULARY, NOT A GUESS. These 17
 * values are the complete set of `entity_type` literals the backend actually
 * persists, enumerated from every `_context.AuditLogs.Add` writer in
 * PROManagementSystem.Application. Each option's `value` is the exact stored
 * string, because the repository filters with `al.entity_type = @EntityType` and
 * Postgres compares text with `=` — so the comparison is CASE-SENSITIVE and
 * "servicefeeinvoice" or "user" silently returns nothing rather than erroring.
 * The `label` is display-only humanisation and is never sent.
 *
 * THIS LIST WILL DRIFT. There is no endpoint that enumerates entity types, so
 * these 17 are a snapshot of the writers that exist today. A type added to the
 * backend later will not appear here, which is precisely why the filter bar also
 * offers an "Other…" free-text escape hatch. Re-derive this list from the audit
 * writers when adding types; never infer it from an unrelated frontend module.
 *
 * THE DATES ARE THE SUBTLE PART. The repository filters
 * `created_at >= @From AND created_at <= @To` against a `timestamptz` column, so
 * BOTH bounds are inclusive. `<input type="date">` yields "YYYY-MM-DD", which
 * ASP.NET would bind to midnight — sending that as `to` would discard every
 * event after 00:00:00 on the chosen day, so a user picking "to 28 Sep" would
 * silently lose the 28th. `toUtcDayEnd` therefore resolves the `to` date to the
 * END of that local calendar day before serialising.
 *
 * `new Date('2026-09-28')` is NOT used to parse these strings: per the ES spec a
 * date-only ISO string is parsed as UTC midnight, which would shift the day for
 * anyone west of Greenwich. The parts are split and fed to the `Date` constructor
 * instead, which builds a LOCAL time, and `.toISOString()` then converts to the
 * explicit UTC instant the query needs. Sending a Z-suffixed instant also avoids
 * leaving Postgres to reinterpret a naive timestamp against the session
 * TimeZone.
 */

/** Exact `entity_type` values the backend persists, with display labels. */
export const ENTITY_TYPE_OPTIONS = [
  { value: 'ClientCompany', label: 'Client company' },
  { value: 'ClientEntity', label: 'Client entity' },
  { value: 'Document', label: 'Document' },
  { value: 'DocumentDependency', label: 'Document dependency' },
  { value: 'DocumentExtraction', label: 'Document extraction' },
  { value: 'DocumentVersion', label: 'Document version' },
  { value: 'Employee', label: 'Employee' },
  { value: 'EmployeeDocument', label: 'Employee document' },
  { value: 'GovFeeDisbursement', label: 'Gov fee disbursement' },
  { value: 'PaymentOrder', label: 'Payment order' },
  { value: 'RenewalStepLog', label: 'Renewal step log' },
  { value: 'RenewalTask', label: 'Renewal task' },
  { value: 'RetainerInvoice', label: 'Retainer invoice' },
  { value: 'ServiceContract', label: 'Service contract' },
  { value: 'ServiceFeeInvoice', label: 'Service fee invoice' },
  { value: 'ServiceRequest', label: 'Service request' },
  { value: 'User', label: 'User' },
]

/** Sentinel for the "Other…" select entry. Never leaves the browser. */
export const OTHER_ENTITY_TYPE = '__other__'

/**
 * Humanises a PascalCase type for display: "ServiceFeeInvoice" becomes
 * "Service fee invoice". A known type uses its curated label; anything else
 * (the free-text override, or a type added to the backend after this file was
 * written) is humanised heuristically rather than shown as a raw identifier.
 */
export function humanizeEntityType(value) {
  if (!value) return null

  const known = ENTITY_TYPE_OPTIONS.find((option) => option.value === value)
  if (known) return known.label

  const spaced = String(value)
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/([A-Z]+)([A-Z][a-z])/g, '$1 $2')
    .trim()
    .toLowerCase()

  return spaced || String(value)
}

/**
 * True when the string is a well-formed GUID. This exists only to keep a typo
 * from becoming a pointless round-trip that returns 400 ProblemDetails; the
 * backend remains the authoritative validator and its error is still surfaced
 * normally if malformed input ever reaches it.
 */
export function isGuid(value) {
  if (typeof value !== 'string') return false
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
    value.trim(),
  )
}

/** Splits "YYYY-MM-DD" into numeric parts without going through Date parsing. */
function toDateParts(value) {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(String(value).trim())
  if (!match) return null
  return {
    year: Number(match[1]),
    month: Number(match[2]) - 1,
    day: Number(match[3]),
  }
}

/**
 * Start of the chosen LOCAL calendar day, serialised as a UTC instant. Returns
 * null for a blank or unparseable value so callers can omit the parameter
 * entirely rather than send an empty string.
 */
export function toUtcDayStart(value) {
  const parts = toDateParts(value)
  if (!parts) return null
  return new Date(parts.year, parts.month, parts.day).toISOString()
}

/**
 * END of the chosen LOCAL calendar day (23:59:59.999), serialised as a UTC
 * instant, so the backend's inclusive `created_at <= @To` covers the whole day
 * the user actually selected.
 */
export function toUtcDayEnd(value) {
  const parts = toDateParts(value)
  if (!parts) return null
  return new Date(
    parts.year,
    parts.month,
    parts.day,
    23,
    59,
    59,
    999,
  ).toISOString()
}
