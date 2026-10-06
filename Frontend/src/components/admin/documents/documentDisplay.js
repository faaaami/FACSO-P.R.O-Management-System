import { DOCUMENT_TYPES, documentStatusLabel, enumLabel } from '../enumLabels'
import { MISSING_VALUE, presentText } from '../clients/clientDisplay'

/**
 * Presentation helpers for the Admin Documents module.
 *
 * This module is the Expiry & Renewal Registry, NOT a global document browser:
 * the backend has no cross-entity Admin document list, so the only multi-row
 * source is GET /admin/documents/expiring. Every field used here was read out of
 * the backend response records.
 *
 *   ExpiringDocumentListItemDto   id, clientEntityId, employeeId, type,
 *                                 documentNumber, issueDate, expiryDate, fileName,
 *                                 isActive, isDeleted, daysRemaining, status
 *   GetDocumentByIdResponseDto    the above + fileUrl, contentType, fileSize,
 *                                 deletedAt, createdAt, updatedAt, details
 *   DocumentVersionListItemDto    id, documentId, versionNumber, fileUrl, fileName,
 *                                 contentType, fileSize, isDeleted, createdAt,
 *                                 updatedAt
 *
 * ------------------------------------------------------------------
 * THREE STATUS VOCABULARIES EXIST. THIS IS THE FILE THAT KEEPS THEM APART.
 * ------------------------------------------------------------------
 *
 *  1. The REGISTRY row (`documents/expiring`) returns `status` as a plain STRING,
 *     produced by SqlSnippets.ExpiryStatusLabelCase with a hardcoded 90-day
 *     threshold. Its closed set is exactly: Expired / ExpiringSoon / Active.
 *     It is mapped by REGISTRY_DOCUMENT_STATUS below.
 *
 *  2. The document DETAIL endpoint returns `status` as the DocumentStatus ENUM
 *     (Active / ExpiringSoon / Overdue / InRenewal), string-serialized globally by
 *     the JsonStringEnumConverter. It is rendered with the shared
 *     documentStatusLabel, which is keyed on that enum.
 *
 *  3. A third variant, DocumentStatusNumericCase, emits integers 1/2/3 for other
 *     read models. Nothing in this module consumes it.
 *
 * THE SAME DOCUMENT THEREFORE READS "Expired" IN THE REGISTRY TABLE AND "Overdue"
 * ON ITS DETAIL RECORD. Both are preserved verbatim, each labelled with the
 * endpoint it came from, and neither is ever converted into the other. There is
 * deliberately no normaliser in this file: a single "canonical" status would have
 * to pick a winner between two real server vocabularies, and that choice would be
 * made in the browser, where it does not belong.
 *
 * THE 90-DAY LABEL IS DECOUPLED FROM THE `days` FILTER. The repository filters with
 * the @Days parameter but labels with a literal 90, so status is NOT a function of
 * the selected window. Two consequences the UI must not fight:
 *   - For any window <= 90 days, "Active" is unreachable — the filter's upper bound
 *     never exceeds the label threshold, so every returned row is Expired or
 *     ExpiringSoon. This is why the page ships no status filter and no status
 *     breakdown chips.
 *   - At days = 90 the entire result set is ExpiringSoon, so the status column
 *     carries no extra information at that window.
 * `daysRemaining` is likewise server-computed and is displayed as sent; the
 * browser never re-derives an expiry window.
 *
 * SHARED PRIMITIVES (MISSING_VALUE, formatDate, formatDateTime, presentText,
 * displayText, truncate) are imported from the completed Admin Clients module
 * rather than reimplemented, per the approved decision to import across module
 * folders instead of refactoring finished Phase 2 files.
 */

/**
 * REGISTRY status vocabulary — the string labels emitted by
 * SqlSnippets.ExpiryStatusLabelCase for GET /admin/documents/expiring.
 *
 * This map is intentionally SEPARATE from enumLabels.DOCUMENT_STATUS, which is
 * keyed on the DocumentStatus enum and has no `Expired` member at all. Reusing it
 * here would fall through to a raw-string fallback for every expired row, which
 * happens to render, but it would be correct by accident rather than by contract.
 *
 * The display label is the API value verbatim: the registry already emits
 * human-readable words, so "ExpiringSoon" is shown as "Expiring soon" for
 * readability while the underlying vocabulary is untouched.
 */
export const REGISTRY_DOCUMENT_STATUS = {
  Active: 'Active',
  ExpiringSoon: 'Expiring Soon',
  Expired: 'Expired',
}

/**
 * Tone for the registry's own status vocabulary. Strictly the three members
 * above. An unrecognised value falls through to 'warning' so a value the backend
 * adds later stays visible instead of reading as a healthy "Active" document.
 */
export function registryStatusTone(status) {
  if (status === 'Active') return 'success'
  if (status === 'ExpiringSoon') return 'warning'
  if (status === 'Expired') return 'danger'
  return 'warning'
}

/** Label for a registry status, falling back to the raw value when unmapped. */
export function registryStatusLabel(status) {
  if (status == null) return null
  return enumLabel(REGISTRY_DOCUMENT_STATUS, status)
}

/**
 * Tone for the STORED document status — the DocumentStatus enum returned by the
 * detail endpoint. This is a different vocabulary from the registry status above,
 * and `Overdue` is mapped here while `Expired` is deliberately not: an unmapped
 * `Expired` reaching this function would mean a registry value had been passed
 * into the detail renderer, and it must not silently adopt the detail palette.
 *
 * An unknown member falls through to 'warning' so it stays visible.
 */
export function storedStatusTone(status) {
  if (status === 'Active') return 'success'
  if (status === 'ExpiringSoon') return 'warning'
  if (status === 'Overdue') return 'danger'
  if (status === 'InRenewal') return 'neutral'
  return 'warning'
}

/**
 * Label for the stored document status. Delegates to the shared enumLabels
 * helper, which is already keyed on DocumentStatus. Not reimplemented here so
 * there is exactly one place that knows the enum's display wording.
 */
export function storedStatusLabel(status) {
  return documentStatusLabel(status)
}

/** Human label for a DocumentType, falling back to the raw value if unmapped. */
export function documentTypeLabel(type) {
  return enumLabel(DOCUMENT_TYPES, type)
}

/**
 * Renders the registry's own `daysRemaining` as neutral arithmetic prose, or null
 * when the endpoint did not send a usable number.
 *
 * THE NUMBER IS THE BACKEND'S. DocumentExpiryReadRepository computes it in SQL as
 * FLOOR(EXTRACT(EPOCH FROM (expiry_date - CURRENT_TIMESTAMP)) / 86400), so it is
 * SIGNED — positive means days until expiry, negative means days since expiry —
 * and it is floored, not rounded. This helper only puts words around that value;
 * it never recomputes it from expiryDate, because a second calculation could
 * disagree with the server's own window by a day at the boundary and the status
 * pill would then contradict the number printed beside it.
 *
 * Because of the floor, 0 means "less than a day left" rather than "today", and -1
 * means "expired less than a day ago". The wording below says exactly that.
 *
 * The output carries NO status meaning and must never be given a tone: the
 * authoritative state is the server-computed `status` field.
 */
export function daysRemainingText(daysRemaining) {
  if (daysRemaining == null) return null
  const days = Number(daysRemaining)
  if (!Number.isFinite(days)) return null

  const whole = Math.trunc(days)
  if (whole > 0) return `${whole} day${whole === 1 ? '' : 's'} to expiry`
  if (whole === 0) return 'Less than a day to expiry'

  const elapsed = Math.abs(whole)
  return `${elapsed} day${elapsed === 1 ? '' : 's'} past expiry`
}

/**
 * Byte size as a short human string, or MISSING_VALUE. `fileSize` is a nullable
 * long on the detail and version DTOs, so its absence is a genuine "not provided"
 * rather than a zero-byte file.
 */
export function formatFileSize(bytes) {
  if (bytes == null || bytes === '') return MISSING_VALUE
  const size = Number(bytes)
  if (!Number.isFinite(size) || size < 0) return MISSING_VALUE
  if (size === 0) return '0 B'

  const units = ['B', 'KB', 'MB', 'GB']
  let value = size
  let unitIndex = 0
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024
    unitIndex += 1
  }
  const rounded = value >= 10 || unitIndex === 0 ? Math.round(value) : value.toFixed(1)
  return `${rounded} ${units[unitIndex]}`
}

/**
 * Pretty-prints the free-form `details` JSON object for display.
 *
 * `details` is typed `object` on the detail DTO and arrives as parsed JSON, so it
 * is rendered as-is rather than interpreted: this module has no schema for it and
 * inventing field labels for an arbitrary blob would be a guess. Returns null for
 * an absent value so the caller can show the real "not provided" dash, and never
 * throws on a value the JSON serializer could not round-trip.
 */
export function detailsJsonText(details) {
  if (details == null) return null
  if (typeof details === 'string') return presentText(details)
  try {
    const text = JSON.stringify(details, null, 2)
    return presentText(text)
  } catch {
    return null
  }
}

/**
 * Shortens a GUID for display as an explicitly UNRESOLVED reference.
 *
 * This exists so an owner that cannot be resolved is still identifiable without
 * being mistaken for a name. The full id stays in the `title` attribute of the
 * element that renders it, and every caller must label the result as unresolved —
 * a bare truncated GUID next to real names would read as a rendering bug.
 */
export function shortGuid(value) {
  const text = presentText(value)
  if (!text) return null
  return text.length > 8 ? `${text.slice(0, 8)}…` : text
}

/**
 * A registry row's document title: "<Type> · <Number>", degrading gracefully when
 * either part is missing rather than emitting a dangling separator.
 */
export function documentTitle(document) {
  const type = documentTypeLabel(document?.type) ?? 'Document'
  const number = presentText(document?.documentNumber)
  return number ? `${type} · ${number}` : type
}
