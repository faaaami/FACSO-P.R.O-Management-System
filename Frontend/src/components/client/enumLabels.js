/**
 * Label maps for enum values returned by the Client Portal API.
 * Labels keyed to the string enum serialization (JsonStringEnumConverter) —
 * e.g. { "Passport": "Passport" } — except payments, whose DTO fields are
 * raw ints and so are keyed numerically.
 * Credits: values verified against the backend source under
 * PROManagementSystem.Domain/Enums.
 */

export const DOCUMENT_TYPES = {
  Passport: 'Passport',
  Visa: 'Visa',
  LaborCard: 'Labor Card',
  EmiratesId: 'Emirates ID',
  TradeLicense: 'Trade License',
  EstablishmentCard: 'Establishment Card',
  Other: 'Other',
}

export const DOCUMENT_STATUS = {
  Active: 'Active',
  ExpiringSoon: 'Expiring Soon',
  Overdue: 'Overdue',
  InRenewal: 'In Renewal',
}

export const SERVICE_CONTRACT_STATUS = {
  Active: 'Active',
  Ended: 'Ended',
}

export const RENEWAL_TASK_STATUS = {
  Submitted: 'Submitted',
  FeePaid: 'Fee Paid',
  AwaitingApproval: 'Awaiting Approval',
  Blocked: 'Blocked',
  Approved: 'Approved',
  Updated: 'Updated',
}

export const SERVICE_REQUEST_TYPES = {
  NewVisa: 'New Visa',
  EarlyRenewal: 'Early Renewal',
  Other: 'Other',
}

export const SERVICE_REQUEST_STATUS = {
  Submitted: 'Submitted',
  Converted: 'Converted',
  Rejected: 'Rejected',
}

export const PAYMENT_INVOICE_TYPES = {
  1: 'Retainer Invoice',
  2: 'Service Fee Invoice',
}

export const PAYMENT_STATUS = {
  1: 'Created',
  2: 'Pending',
  3: 'Paid',
  4: 'Failed',
  5: 'Cancelled',
  6: 'Refunded',
}

export function enumLabel(map, value) {
  if (value == null) return null
  return map[value] ?? String(value)
}