/**
 * Label maps for the enum values returned by the Admin Portal API.
 *
 * WIRE FORMAT — verified, not assumed:
 * `PROManagementSystem/Program.cs` registers `new JsonStringEnumConverter()`
 * globally, so every enum-typed DTO property is serialized as its *name*
 * (e.g. "Submitted", "Pending"). Maps below are therefore keyed by enum name.
 *
 * The two exceptions are PaymentOrderDto.InvoiceType and PaymentOrderDto.Status,
 * which are declared as `int` on the DTO itself and so arrive as numbers. Those
 * maps are keyed numerically and are kept separate so the inconsistency stays
 * visible instead of being smoothed over.
 *
 * ORDINALS — `*_VALUES` records hold the verified integer values. They exist for
 * ordering/deriving workflow positions, NOT for display. Verified against
 * PROManagementSystem.Domain/Enums.
 */

/** RenewalTaskStatus — Submitted=1, FeePaid=2, AwaitingApproval=3, Blocked=4, Approved=5, Updated=6. */
export const RENEWAL_TASK_STATUS = {
  Submitted: 'Submitted',
  FeePaid: 'Fee Paid',
  AwaitingApproval: 'Awaiting Approval',
  Blocked: 'Blocked',
  Approved: 'Approved',
  Updated: 'Updated',
}

export const RENEWAL_TASK_STATUS_VALUES = {
  Submitted: 1,
  FeePaid: 2,
  AwaitingApproval: 3,
  Blocked: 4,
  Approved: 5,
  Updated: 6,
}

/**
 * The backend's forward-only workflow chain (UpdateRenewalTaskStatusCommandHandler).
 * Blocked is deliberately absent: it is reached through PATCH /admin/tasks/{id}/block
 * and is explicitly rejected by the status endpoint.
 */
export const RENEWAL_TASK_STATUS_CHAIN = [
  'Submitted',
  'FeePaid',
  'AwaitingApproval',
  'Approved',
  'Updated',
]

/** ServiceRequestStatus — Submitted=1, Converted=2, Rejected=3. */
export const SERVICE_REQUEST_STATUS = {
  Submitted: 'Submitted',
  Converted: 'Converted',
  Rejected: 'Rejected',
}

export const SERVICE_REQUEST_STATUS_VALUES = {
  Submitted: 1,
  Converted: 2,
  Rejected: 3,
}

/** ServiceRequestType — NewVisa=1, EarlyRenewal=2, Other=3. */
export const SERVICE_REQUEST_TYPES = {
  NewVisa: 'New Visa',
  EarlyRenewal: 'Early Renewal',
  Other: 'Other',
}

export const SERVICE_REQUEST_TYPE_VALUES = {
  NewVisa: 1,
  EarlyRenewal: 2,
  Other: 3,
}

/** ServiceContractStatus — Active=1, Ended=2. */
export const SERVICE_CONTRACT_STATUS = {
  Active: 'Active',
  Ended: 'Ended',
}

export const SERVICE_CONTRACT_STATUS_VALUES = {
  Active: 1,
  Ended: 2,
}

/** RetainerInvoiceStatus / ServiceFeeInvoiceStatus — Pending=1, Paid=2, Void=3. */
export const RETAINER_INVOICE_STATUS = {
  Pending: 'Pending',
  Paid: 'Paid',
  Void: 'Void',
}

export const SERVICE_FEE_INVOICE_STATUS = {
  Pending: 'Pending',
  Paid: 'Paid',
  Void: 'Void',
}

export const INVOICE_STATUS_VALUES = {
  Pending: 1,
  Paid: 2,
  Void: 3,
}

/** GovFeeDisbursementStatus — string-serialized. */
export const GOV_FEE_DISBURSEMENT_STATUS = {
  PaidByFirm: 'Paid by Firm',
  InvoicedToClient: 'Invoiced to Client',
  Reimbursed: 'Reimbursed',
}

/**
 * PaymentInvoiceType — 1=RetainerInvoice, 2=ServiceFeeInvoice.
 * Numeric: PaymentOrderDto.InvoiceType is declared `int`, so this arrives as a number.
 */
export const PAYMENT_INVOICE_TYPES = {
  1: 'Retainer Invoice',
  2: 'Service Fee Invoice',
}

/**
 * PaymentStatus — 1=Created, 2=Pending, 3=Paid, 4=Failed, 5=Cancelled, 6=Refunded.
 * Numeric: PaymentOrderDto.Status is declared `int`, so this arrives as a number.
 */
export const PAYMENT_STATUS = {
  1: 'Created',
  2: 'Pending',
  3: 'Paid',
  4: 'Failed',
  5: 'Cancelled',
  6: 'Refunded',
}

/** DocumentStatus / DocumentType arrive string-serialized like every other enum. */
export const DOCUMENT_STATUS = {
  Active: 'Active',
  ExpiringSoon: 'Expiring Soon',
  Overdue: 'Overdue',
  InRenewal: 'In Renewal',
}

export const DOCUMENT_TYPES = {
  Passport: 'Passport',
  Visa: 'Visa',
  LaborCard: 'Labor Card',
  EmiratesId: 'Emirates ID',
  TradeLicense: 'Trade License',
  EstablishmentCard: 'Establishment Card',
  Other: 'Other',
}

/** UserRole — string-serialized. */
export const USER_ROLES = {
  Admin: 'Admin',
  Agent: 'Agent',
  Client: 'Client',
}

/**
 * Resolves a label for `value`, falling back to the raw value so an unmapped or
 * unexpected enum member is still visible rather than silently blank.
 */
export function enumLabel(map, value) {
  if (value == null) return null
  return map[value] ?? String(value)
}

/** Convenience wrappers for the maps used across several Admin modules. */
export const renewalTaskStatusLabel = (value) => enumLabel(RENEWAL_TASK_STATUS, value)
export const serviceRequestStatusLabel = (value) => enumLabel(SERVICE_REQUEST_STATUS, value)
export const serviceRequestTypeLabel = (value) => enumLabel(SERVICE_REQUEST_TYPES, value)
export const serviceContractStatusLabel = (value) => enumLabel(SERVICE_CONTRACT_STATUS, value)
export const retainerInvoiceStatusLabel = (value) => enumLabel(RETAINER_INVOICE_STATUS, value)
export const serviceFeeInvoiceStatusLabel = (value) => enumLabel(SERVICE_FEE_INVOICE_STATUS, value)
export const paymentStatusLabel = (value) => enumLabel(PAYMENT_STATUS, value)
export const paymentInvoiceTypeLabel = (value) => enumLabel(PAYMENT_INVOICE_TYPES, value)
export const documentStatusLabel = (value) => enumLabel(DOCUMENT_STATUS, value)
