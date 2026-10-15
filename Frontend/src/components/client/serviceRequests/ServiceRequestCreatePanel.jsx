import { useState } from 'react'
import { AlertCircle, CheckCircle2, Loader2, Send } from 'lucide-react'
import { useCreateClientServiceRequest } from '../../../hooks/client/useCreateClientServiceRequest'
import { useClientEmployees } from '../../../hooks/client/useClientEmployees'
import { useClientEntities } from '../../../hooks/client/useClientEntities'
import { SERVICE_REQUEST_TYPES } from '../enumLabels'

const PAGE_SIZE = 100

/**
 * THE ONLY SERVICE A CLIENT CAN ACTUALLY REQUEST.
 *
 * ServiceRequestType still has three members — NewVisa, EarlyRenewal and Other —
 * and SERVICE_REQUEST_TYPES still labels all three, because requests already
 * recorded under the other two must keep displaying their real type. The enum is
 * not being changed here.
 *
 * What is limited is what can be CREATEd, and only EarlyRenewal is offered,
 * because it is the only type the workflow can carry to completion:
 * CreateServiceRequestCommandHandler resolves a linked document only for
 * EarlyRenewal, and ConvertServiceRequestCommandHandler refuses any request with no
 * linked document. A client raising a NewVisa or Other request today gets a
 * request that is accepted, sits in the queue, and can only ever be rejected —
 * which is a worse outcome than telling them at the point of submission that the
 * service is not offered here.
 *
 * Kept as an explicit list rather than a filter over SERVICE_REQUEST_TYPES so that
 * adding a member to the enum cannot silently re-open this selector.
 */
const SUPPORTED_SERVICE_TYPES = ['EarlyRenewal']

const inputClasses =
  'h-10 w-full rounded-[10px] border border-[#E2E4E9] bg-white px-3.5 py-2 text-sm text-[#16181D] focus:border-[#0F9D74] focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] focus:outline-none transition duration-150'

function Field({ label, children, hint }) {
  return (
    <label className="flex flex-col gap-1.5">
      <span className="text-xs font-medium text-[#6B7280]">{label}</span>
      {children}
      {hint && <span className="text-[11px] text-[#9CA3AF]">{hint}</span>}
    </label>
  )
}

function subjectKindLabel(kind) {
  return kind === 'employee' ? 'Employee' : 'Entity'
}

function ServiceRequestCreatePanel() {
  const [type, setType] = useState('')
  const [subjectKind, setSubjectKind] = useState('employee')
  const [employeeId, setEmployeeId] = useState('')
  const [entityId, setEntityId] = useState('')
  const [notes, setNotes] = useState('')

  const {
    data: employeesData,
    isLoading: employeesLoading,
    isError: employeesError,
  } = useClientEmployees({ page: 1, pageSize: PAGE_SIZE })

  const {
    data: entitiesData,
    isLoading: entitiesLoading,
    isError: entitiesError,
  } = useClientEntities({ page: 1, pageSize: PAGE_SIZE })

  const mutation = useCreateClientServiceRequest()

  const employees = employeesData?.items ?? []
  const entities = entitiesData?.items ?? []

  const subjectLoadError = subjectKind === 'employee' ? employeesError : entitiesError

  const canSubmit =
    Boolean(type) &&
    (subjectKind === 'employee' ? Boolean(employeeId) : Boolean(entityId)) &&
    !mutation.isPending

  const resetForm = () => {
    setType('')
    setSubjectKind('employee')
    setEmployeeId('')
    setEntityId('')
    setNotes('')
  }

  const handleSubmit = (event) => {
    event.preventDefault()
    if (!canSubmit) return

    mutation.mutate(
      {
        type,
        employeeId: subjectKind === 'employee' ? employeeId || undefined : undefined,
        entityId: subjectKind === 'entity' ? entityId || undefined : undefined,
        notes: notes.trim() || undefined,
      },
      { onSuccess: resetForm }
    )
  }

  const apiError = mutation.error?.response?.data?.error
  const errorCode = apiError?.code
  const errorMessage = apiError?.message

  const validationDetails =
    errorCode === 'VALIDATION_ERROR' && apiError?.details
      ? Object.entries(apiError.details)
          .flatMap(([, messages]) => messages)
          .join(' ')
      : null

  const errorText =
    validationDetails ??
    errorMessage ??
    'The service request could not be submitted.'

  return (
    <section aria-label="Submit a service request" className="rounded-[12px] bg-white border border-[#E2E4E9] p-6 shadow-[0_1px_3px_rgba(28,31,38,0.06)]">
      <div className="flex items-center gap-3 mb-5">
        <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#16181D]">
          <Send size={18} strokeWidth={1.75} aria-hidden="true" />
        </div>
        <div>
          <h2 className="text-base font-semibold tracking-tight text-[#16181D]">New Service Request</h2>
          <p className="text-xs text-[#6B7280]">Submit a new request to the PRO team</p>
        </div>
      </div>

      {mutation.isSuccess && (
        <div className="mb-4 flex items-start gap-2 rounded-[8px] bg-[rgba(15,157,116,0.08)] p-3 text-xs font-medium text-[#0F9D74] border border-[#0F9D74]/20">
          <CheckCircle2 size={14} className="shrink-0 mt-0.5" aria-hidden="true" />
          <span>Service request submitted. The PRO team will review it shortly.</span>
        </div>
      )}

      {mutation.isError && (
        <div className="mb-4 flex items-start gap-2 rounded-[8px] bg-red-50 p-3 text-xs font-medium text-[#DC2626] border border-red-200">
          <AlertCircle size={14} className="shrink-0 mt-0.5" aria-hidden="true" />
          <span>{errorText}</span>
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <Field
          label="Service Type"
          hint="Early renewal attaches the employee or entity's active visa automatically, so the request can be turned into a renewal task"
        >
          <select
            className={inputClasses}
            value={type}
            onChange={(e) => setType(e.target.value)}
            disabled={mutation.isPending}
          >
            <option value="">Select a service type</option>
            {SUPPORTED_SERVICE_TYPES.map((key) => (
              <option key={key} value={key}>
                {SERVICE_REQUEST_TYPES[key]}
              </option>
            ))}
          </select>
        </Field>

        <Field label="Subject">
          <div className="flex items-center gap-1 rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA] p-1">
            {['employee', 'entity'].map((kind) => (
              <button
                key={kind}
                type="button"
                onClick={() => setSubjectKind(kind)}
                disabled={mutation.isPending}
                className={`flex-1 rounded-[8px] px-3 py-1.5 text-xs font-semibold transition duration-150 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] cursor-pointer ${
                  subjectKind === kind
                    ? 'bg-white text-[#16181D] shadow-[0_1px_2px_rgba(28,31,38,0.08)] border border-[#E2E4E9]'
                    : 'text-[#6B7280] hover:text-[#16181D]'
                }`}
              >
                {subjectKindLabel(kind)}
              </button>
            ))}
          </div>
        </Field>

        {subjectKind === 'employee' ? (
          <Field label="Employee">
            <select
              className={inputClasses}
              value={employeeId}
              onChange={(e) => setEmployeeId(e.target.value)}
              disabled={mutation.isPending}
            >
              <option value="">Select an employee</option>
              {employeesLoading && <option disabled>Loading employees…</option>}
              {!employeesLoading && !employeesError && employees.map((emp) => (
                <option key={emp.id} value={emp.id}>
                  {emp.fullName}
                  {emp.entityName ? ` — ${emp.entityName}` : ''}
                </option>
              ))}
            </select>
          </Field>
        ) : (
          <Field label="Entity">
            <select
              className={inputClasses}
              value={entityId}
              onChange={(e) => setEntityId(e.target.value)}
              disabled={mutation.isPending}
            >
              <option value="">Select an entity</option>
              {entitiesLoading && <option disabled>Loading entities…</option>}
              {!entitiesLoading && !entitiesError && entities.map((entity) => (
                <option key={entity.id} value={entity.id}>
                  {entity.entityName}
                </option>
              ))}
            </select>
          </Field>
        )}

        {subjectLoadError && (
          <p className="text-xs text-[#DC2626]">Subjects could not be loaded. Retry by reopening this panel.</p>
        )}

        <Field label="Notes (optional)" hint="Maximum 2000 characters">
          <textarea
            className="w-full min-h-[96px] rounded-[10px] border border-[#E2E4E9] bg-white px-3.5 py-2.5 text-sm text-[#16181D] focus:border-[#0F9D74] focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] focus:outline-none transition duration-150 resize-y"
            value={notes}
            maxLength={2000}
            onChange={(e) => setNotes(e.target.value)}
            disabled={mutation.isPending}
            placeholder="Add context for the PRO team…"
          />
        </Field>

        <button
          type="submit"
          disabled={!canSubmit}
          className="inline-flex w-full items-center justify-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] hover:bg-[#101319] disabled:opacity-50 disabled:cursor-not-allowed transition duration-150 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] cursor-pointer"
        >
          {mutation.isPending ? (
            <>
              <Loader2 size={16} strokeWidth={2} className="animate-spin" aria-hidden="true" />
              Submitting…
            </>
          ) : (
            <>
              <Send size={16} strokeWidth={2} aria-hidden="true" />
              Submit Request
            </>
          )}
        </button>
      </form>
    </section>
  )
}

export default ServiceRequestCreatePanel