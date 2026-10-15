import { format } from 'date-fns'

export function formatMoney(amount, currency) {
  if (amount == null || Number.isNaN(Number(amount))) return 'N/A'
  try {
    return new Intl.NumberFormat('en-AE', {
      style: 'currency',
      currency: currency ?? 'AED',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(Number(amount))
  } catch {
    return `${currency ?? 'AED'} ${Number(amount).toFixed(2)}`
  }
}

export function formatDate(value) {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  return format(date, 'dd MMM yyyy')
}

export function formatDateTime(value) {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  return format(date, 'dd MMM yyyy, HH:mm')
}