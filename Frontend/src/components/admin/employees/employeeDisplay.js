import { DOCUMENT_TYPES, enumLabel } from '../enumLabels'
import { presentText } from '../clients/clientDisplay'

/**
 * Presentation helpers for the Admin Employees module.
 *
 * Every field name used here was read out of the backend response records, not
 * assumed. Anything a DTO does not carry is a backend gap to document, never
 * something to synthesise in the browser:
 *
 *   GetEmployeeListItemDto     id, clientEntityId, entityName, fullName,
 *                              passportNumber, nationality, jobTitle, hireDate,
 *                              isActive, isDeleted, createdAt
 *   GetEmployeeByIdResponseDto the above + clientCompanyId, dateOfBirth,
 *                              updatedAt
 *   EmployeeDocumentListItem   id, employeeId, type, documentNumber, issueDate,
 *                              expiryDate, fileUrl, fileName, contentType,
 *                              fileSize, isActive, isDeleted, createdAt,
 *                              updatedAt, status
 *   EmployeeTimelineItemDto    id, action, entityType, entityId, description,
 *                              createdAt
 *
 * Facts that constrain this module and are easy to get wrong:
 *
 *  - The LIST DTO has no clientCompanyId. A list row therefore cannot be linked
 *    to a company, and no helper here may pretend otherwise. Only the detail DTO
 *    carries a company id, and it carries no company NAME — resolving that name
 *    is a separate request (see AdminEmployeeProfileSection).
 *  - The list DTO has no dateOfBirth and no updatedAt, so neither can appear in
 *    the table.
 *  - The list endpoint hard-filters is_deleted = false, so `isDeleted` is always
 *    false on a row. The tone helper still handles it, because the DETAIL
 *    endpoint does return a genuinely deleted employee.
 *  - The list is hardcoded to created_at DESC. There is no sort parameter, so
 *    nothing in the UI may imply a sortable column.
 *  - An employee document `status` is the DocumentStatus enum, string-serialized
 *    globally by the JsonStringEnumConverter, and is computed server-side by
 *    SqlSnippets.DocumentStatusNumericCase on a 90-day window. It is NOT the
 *    same vocabulary as the expiring endpoints, which emit the plain labels
 *    Active / ExpiringSoon / Expired. `Overdue` is therefore rendered as
 *    "Overdue" and is never relabelled "Expired" — the expiring endpoints are
 *    deferred out of this phase and must not leak in through a tone mapping.
 *  - A timeline `action` / `entityType` is a free-form string written by command
 *    handlers, not an enum. There is no closed set to switch over, so the label
 *    helpers only reformat whatever arrived and fall back to the raw value.
 *
 * Shared primitives (MISSING_VALUE, formatDate, formatDateTime, presentText,
 * displayText, truncate) are imported from the Admin Clients module rather than
 * reimplemented, per the approved decision to import across module folders
 * rather than refactor the completed Phase 2 files.
 */

/** Fallback when an employee has no usable fullName. */
export function employeeName(employee) {
  return presentText(employee?.fullName) ?? 'Unnamed employee'
}

/**
 * Deleted outranks inactive, matching the rest of the Admin surface: a
 * soft-deleted employee is described as Deleted rather than Inactive.
 * Reachable through the detail endpoint, never through the list.
 */
export function employeeStatusTone(employee) {
  if (employee?.isDeleted) return 'danger'
  return employee?.isActive ? 'success' : 'neutral'
}

export function employeeStatusLabel(employee) {
  if (employee?.isDeleted) return 'Deleted'
  return employee?.isActive ? 'Active' : 'Inactive'
}

/**
 * Tenure derived from hireDate, e.g. "3 yr 2 mo".
 *
 * DERIVED, NOT A BACKEND FIELD. The employee DTOs carry no tenure, so this is
 * computed once here rather than in each section. It is intentionally the only
 * value in this file derived from a date for display purposes, and it is
 * labelled as derived wherever it is rendered. Returns null when hireDate is
 * absent or unparseable so the caller can show the real "not provided" dash.
 */
export function employeeTenure(hireDate) {
  if (!hireDate) return null
  const hired = new Date(hireDate)
  if (Number.isNaN(hired.getTime())) return null

  const now = new Date()
  let months =
    (now.getFullYear() - hired.getFullYear()) * 12 +
    (now.getMonth() - hired.getMonth())
  if (now.getDate() < hired.getDate()) months -= 1
  if (months < 0) return null

  const years = Math.floor(months / 12)
  const remainder = months % 12

  if (years === 0) return `${remainder} mo`
  if (remainder === 0) return `${years} yr`
  return `${years} yr ${remainder} mo`
}

/** Human label for a DocumentType, falling back to the raw value if unmapped. */
export function documentTypeLabel(type) {
  return enumLabel(DOCUMENT_TYPES, type)
}

/**
 * Tone for the DocumentStatus enum returned by the employee documents endpoint.
 * Strictly the enum vocabulary: Active / ExpiringSoon / Overdue / InRenewal.
 * "Expired" is deliberately NOT handled — it belongs to the deferred expiring
 * endpoints and must not be conflated with Overdue. An unrecognised member falls
 * through to warning so it stays visible rather than reading as Active.
 */
export function documentStatusTone(status) {
  if (status === 'Active') return 'success'
  if (status === 'ExpiringSoon') return 'warning'
  if (status === 'Overdue') return 'danger'
  if (status === 'InRenewal') return 'neutral'
  return 'warning'
}

/**
 * Whole days from today until an expiry date: positive in the future, negative
 * in the past, 0 today, null when there is no usable date.
 *
 * A neutral arithmetic fact only. It carries NO status meaning and must never be
 * given a tone: the document's state comes from the server-computed `status`
 * field, and deriving our own expiry window here would duplicate backend rules
 * and disagree with them (the status projection uses a 90-day window; the
 * expiring endpoints use a caller-supplied `days` window that defaults to 30).
 */
export function daysUntil(dateValue) {
  if (!dateValue) return null
  const target = new Date(dateValue)
  if (Number.isNaN(target.getTime())) return null

  const startOfToday = new Date()
  startOfToday.setHours(0, 0, 0, 0)
  const startOfTarget = new Date(target)
  startOfTarget.setHours(0, 0, 0, 0)

  return Math.round((startOfTarget - startOfToday) / 86400000)
}

/**
 * Renders daysUntil as neutral arithmetic prose, or null when there is nothing to
 * say.
 *
 * WORDING IS DELIBERATELY STATUS-FREE. This says "12 days to expiry" or
 * "3 days past expiry" and nothing more. It never says Expired, Expiring Soon or
 * Overdue, because those are status words owned by the server-computed `status`
 * field — and the expiring endpoints' separate "Expired" label belongs to a
 * capability deferred out of this phase. Mixing the two vocabularies in one card
 * would make it look as though the browser had invented its own expiry rules.
 */
export function daysRemainingText(dateValue) {
  const days = daysUntil(dateValue)
  if (days == null) return null
  if (days === 0) return 'Expires today'
  if (days > 0) return `${days} day${days === 1 ? '' : 's'} to expiry`
  const elapsed = Math.abs(days)
  return `${elapsed} day${elapsed === 1 ? '' : 's'} past expiry`
}

/**
 * Reformats a free-form action string such as "EmployeeDeleted" into readable
 * prose. There is no closed enum behind this field, so nothing is invented
 * here: a blank value yields null, and anything unrecognised is shown verbatim
 * rather than being mapped to a guessed event name.
 */
export function timelineActionLabel(action) {
  const text = presentText(action)
  if (!text) return null
  return splitPascalCase(text)
}

/** Same treatment for the free-form entityType on a timeline row. */
export function timelineEntityLabel(entityType) {
  const text = presentText(entityType)
  if (!text) return null
  return splitPascalCase(text)
}

function splitPascalCase(value) {
  return value
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/[_-]+/g, ' ')
    .trim()
}
