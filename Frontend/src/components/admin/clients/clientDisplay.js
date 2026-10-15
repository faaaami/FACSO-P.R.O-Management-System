/**
 * Presentation helpers for the Admin Clients module.
 *
 * Every field name used here was read out of the backend response records, not
 * assumed. If a helper needs a value that no DTO carries, that is a backend gap
 * to document — not something to synthesise in the browser:
 *
 *   ClientCompanyListItemDto  id, companyName, tradeLicenseNumber, phone, email,
 *                             emirate, isActive, isDeleted, createdAt
 *   GetClientCompanyById…Dto  the above + address, updatedAt
 *   GetClientEntityListItem…  id, clientCompanyId, entityName, tradeLicenseNumber,
 *                             emirate, isActive, isDeleted, createdAt
 *   GetClientEntityById…Dto   the above + updatedAt
 *   GetClientContactListItem… id, clientCompanyId, fullName, email, phone, role,
 *                             isActive, isApproved, isDeleted, createdAt
 *   GetEmployeeListItemDto    id, clientEntityId, entityName, fullName,
 *                             passportNumber, nationality, jobTitle, hireDate,
 *                             isActive, isDeleted, createdAt
 *   ServiceContractListItem…  id, clientCompanyId, contractNumber, startDate,
 *                             endDate, status, retainerAmount, terms
 *
 * Consequences worth remembering while editing this file:
 *   - A contact has NO position and NO primary-contact flag.
 *   - An entity has NO address, email, phone, establishment card or licence dates.
 *   - A company has only isActive and isDeleted — no approved/pending state.
 *   - Nothing carries a deletedAt, so "deleted" can only be reported as a flag.
 */

/** Shown wherever a value is genuinely absent. Never used for a failed request. */
export const MISSING_VALUE = '—'

/**
 * Formats a date for display, or returns MISSING_VALUE when it is absent or
 * unparseable. Used for real "not provided" values only — never as a stand-in
 * for a request that failed, which must render an error with a retry instead.
 */
export function formatDate(value) {
  if (!value) return MISSING_VALUE
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return MISSING_VALUE
  return parsed.toLocaleDateString('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  })
}

export function formatDateTime(value) {
  if (!value) return MISSING_VALUE
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return MISSING_VALUE
  return `${parsed.toLocaleDateString('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  })} · ${parsed.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}`
}

/**
 * Retainer amounts are decimals and always present on a contract, so a missing
 * one means the record is malformed rather than the field being optional.
 */
export function formatCurrency(value) {
  if (value == null || value === '') return MISSING_VALUE
  const amount = Number(value)
  if (!Number.isFinite(amount)) return MISSING_VALUE
  return `AED ${amount.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`
}

/** Trims a nullable string, returning null for anything blank. */
export function presentText(value) {
  const trimmed = typeof value === 'string' ? value.trim() : ''
  return trimmed ? trimmed : null
}

/**
 * Renders an optional value, or MISSING_VALUE when it is absent. Keeps the
 * "is it empty string or null" question in one place so sections do not each
 * reimplement it and disagree about what a blank cell means.
 */
export function displayText(value) {
  return presentText(value) ?? MISSING_VALUE
}

/**
 * Tones are decorative only. Every pill in this module ships with its text
 * label, so state is never communicated by colour alone.
 */
export const TONE_CLASSES = {
  neutral: 'border-[#E2E4E9] bg-[#F7F8FA] text-[#6B7280]',
  success: 'border-[#0F9D74]/25 bg-[rgba(15,157,116,0.10)] text-[#0B7A5A]',
  warning: 'border-[#B45309]/25 bg-[rgba(180,83,9,0.10)] text-[#92400E]',
  danger: 'border-[#DC2626]/25 bg-[rgba(220,38,38,0.10)] text-[#B91C1C]',
}

/**
 * A deleted record outranks an inactive one: a soft-deleted company is described
 * as Deleted rather than Active/Inactive, because isActive stays true on rows
 * that were only archived.
 */
export function recordStatusTone(record) {
  if (record?.isDeleted) return 'danger'
  return record?.isActive ? 'success' : 'neutral'
}

export function recordStatusLabel(record) {
  if (record?.isDeleted) return 'Deleted'
  return record?.isActive ? 'Active' : 'Inactive'
}

/**
 * A contact carries an approval state that the company itself does not have, so
 * it gets its own resolver rather than being forced through recordStatusLabel.
 * An inactive contact is reported as Inactive even when approved, because
 * inactivity is the more actionable fact.
 */
export function contactStatusTone(contact) {
  if (contact?.isDeleted) return 'danger'
  if (!contact?.isActive) return 'neutral'
  return contact?.isApproved ? 'success' : 'warning'
}

export function contactStatusLabel(contact) {
  if (contact?.isDeleted) return 'Deleted'
  if (!contact?.isActive) return 'Inactive'
  return contact?.isApproved ? 'Approved' : 'Awaiting approval'
}

/**
 * Service contracts are Active=1 / Ended=2, string-serialized globally by the
 * JsonStringEnumConverter. A value outside that set is shown as-is rather than
 * coerced, so an unexpected member is visible instead of silently blank.
 */
export function contractStatusTone(status) {
  if (status === 'Active') return 'success'
  if (status === 'Ended') return 'neutral'
  return 'warning'
}

/**
 * A contract is only meaningfully "current" if it is Active AND has not already
 * passed its end date. The backend's own "active" concept ignores dates, so this
 * comparison is what stops the UI from presenting a lapsed contract as current.
 */
export function isContractCurrent(contract) {
  if (contract?.status !== 'Active') return false
  if (!contract?.endDate) return true
  const end = new Date(contract.endDate)
  if (Number.isNaN(end.getTime())) return false
  return end.getTime() >= Date.now()
}

/** Shortens a long name for a table cell, keeping the full value in `title`. */
export function truncate(value, max = 46) {
  const text = presentText(value)
  if (!text) return null
  return text.length > max ? `${text.slice(0, max - 1)}…` : text
}
