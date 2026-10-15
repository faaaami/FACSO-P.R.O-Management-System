import { format } from 'date-fns'
import { DOCUMENT_STATUS, DOCUMENT_TYPES, enumLabel } from '../../client/enumLabels'

/** Human label for a document type. */
export function documentTypeLabel(type) {
  return enumLabel(DOCUMENT_TYPES, type) ?? 'Document'
}

/** Human label for a document status. */
export function documentStatusLabel(status) {
  return enumLabel(DOCUMENT_STATUS, status) ?? String(status ?? 'Unknown')
}

/** Maps a document status to a StatusPill tone. */
export function documentStatusTone(status) {
  switch (status) {
    case 'Active':
      return 'success'
    case 'ExpiringSoon':
    case 'InRenewal':
      return 'warning'
    case 'Overdue':
      return 'danger'
    default:
      return 'neutral'
  }
}

/** Formats a date for display; returns null when absent/invalid. */
export function formatDate(value) {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  return format(date, 'dd MMM yyyy')
}

/** Formats a date+time for display; returns null when absent/invalid. */
export function formatDateTime(value) {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  return format(date, 'dd MMM yyyy, HH:mm')
}

/** Formats a byte count as a human-readable size. */
export function formatFileSize(bytes) {
  if (typeof bytes !== 'number' || Number.isNaN(bytes)) return null
  if (bytes < 1024) return `${bytes} B`
  const units = ['KB', 'MB', 'GB']
  let value = bytes / 1024
  let unitIndex = 0
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024
    unitIndex += 1
  }
  return `${value.toFixed(value >= 10 ? 0 : 1)} ${units[unitIndex]}`
}

/**
 * Renders one free-form `details` value using its schema valueKind.
 * DocumentFieldValueKind is a string enum: 'String' | 'Date' | 'Number' |
 * 'Boolean'. Unknown kinds fall back to a plain string.
 */
export function formatDetailsValue(value, valueKind) {
  if (value === null || value === undefined || value === '') return '—'
  switch (valueKind) {
    case 'Date': {
      if (typeof value !== 'string') return String(value)
      const parsed = new Date(value)
      return Number.isNaN(parsed.getTime()) ? value : format(parsed, 'dd MMM yyyy')
    }
    case 'Number':
      return typeof value === 'number' ? String(value) : String(value)
    case 'Boolean':
      return value ? 'Yes' : 'No'
    default:
      if (typeof value === 'boolean') return value ? 'Yes' : 'No'
      if (Array.isArray(value)) return value.join(', ')
      if (typeof value === 'object') return JSON.stringify(value)
      return String(value)
  }
}
