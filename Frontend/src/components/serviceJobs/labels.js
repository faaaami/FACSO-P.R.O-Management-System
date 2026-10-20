export const CATEGORY_LABELS = {
  Visa: 'Visa & residence',
  EmiratesId: 'Emirates ID',
  Labour: 'Labour (MOHRE)',
  CompanyLicensing: 'Company & licensing',
  Typing: 'Typing & translation',
  Attestation: 'Attestation',
  MedicalInsurance: 'Medical & insurance',
  Other: 'Other',
}
export const CATEGORIES = Object.keys(CATEGORY_LABELS)

export const JOB_STATUS = {
  New: { label: 'New', tone: 'neutral' },
  InProgress: { label: 'In progress', tone: 'success' },
  OnHold: { label: 'On hold', tone: 'warning' },
  Completed: { label: 'Completed', tone: 'success' },
  Cancelled: { label: 'Cancelled', tone: 'danger' },
}

export const STEP_STATUS = {
  Pending: { label: 'Pending', tone: 'neutral' },
  Done: { label: 'Done', tone: 'success' },
  Skipped: { label: 'Skipped', tone: 'warning' },
}

export const CUSTOMER_TYPE_LABELS = {
  Subscription: 'Subscription',
  WalkInCompany: 'Walk-in company',
  WalkInIndividual: 'Walk-in individual',
}

export const PERSON_TYPE_LABELS = {
  Employee: 'Employee',
  PartnerInvestor: 'Partner / investor',
  Dependent: 'Dependent',
  DomesticWorker: 'Domestic worker',
  Individual: 'Individual',
}

export const SUBJECT_LABELS = { Person: 'Per person', Company: 'Per company', None: 'No subject' }

export const SOURCE_LABELS = { Counter: 'Counter', ClientPortal: 'Client portal', Staff: 'Staff' }

export const DOCUMENT_TYPE_LABELS = {
  Passport: 'Passport',
  Visa: 'Visa',
  LaborCard: 'Labour card',
  EmiratesId: 'Emirates ID',
  TradeLicense: 'Trade licence',
  EstablishmentCard: 'Establishment card',
  Other: 'Other',
}

export function formatAed(value) {
  if (value === null || value === undefined) return '—'
  return `AED ${Number(value).toLocaleString('en-AE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

export function formatDate(value) {
  if (!value) return '—'
  const date = typeof value === 'string' && value.length === 10 ? new Date(`${value}T00:00:00`) : new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return date.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

export function formatDateTime(value) {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return date.toLocaleString('en-GB', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
}

export const isOpenStatus = (status) => status === 'New' || status === 'InProgress' || status === 'OnHold'
