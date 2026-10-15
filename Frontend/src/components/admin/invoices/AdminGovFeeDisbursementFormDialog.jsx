import { useEffect, useId, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, AlertTriangle, Loader2 } from 'lucide-react'
import FormField, { inputClass, inputErrorClass } from '../../client/settings/FormField'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import { useAdminBillingFilterOptions } from '../../../hooks/admin/useAdminBillingFilterOptions'
import { useCreateAdminGovFeeDisbursement } from '../../../hooks/admin/useAdminBillingMutations'
import { apiFieldErrors } from '../../../utils/apiFieldErrors'
import { extractApiErrorMessage } from '../../../utils/apiError'
import {
  AMOUNT_RULE_NOTE,
  BILLING_FIELD_LIMITS,
  CURRENCY_FIELD_NOTE,
  ZERO_AMOUNT_WARNING,
  presentText,
} from './billingDisplay'

/**
 * Create dialog for a government-fee disbursement — POST /api/v1/gov-fee-disbursements.
 *
 * ------------------------------------------------------------------
 * THIS IS NOT AN INVOICE, SO IT HAS NONE OF AN INVOICE'S FIELDS.
 * ------------------------------------------------------------------
 * A disbursement is the firm having PAID a government fee and waiting to be
 * reimbursed. CreateGovFeeDisbursementRequest has no invoice number, no invoice
 * date, no due date and no status field, and so this form has none of them:
 *
 *   clientCompanyId      required  -> company picker
 *   renewalTaskId        OPTIONAL  -> task picker, and genuinely optional
 *   feeDescription       required, max 2000
 *   governmentReference  optional, UNBOUNDED
 *   amount               required, >= 0
 *   currency             required, free text
 *   notes                optional, UNBOUNDED
 *
 * THE STATUS IS NOT A FIELD, because the handler assigns it: hard-set to PaidByFirm
 * with PaidByFirmAt = UtcNow and the other two timestamps null. A status control here
 * could only ever submit a value the server ignores, so the form states the starting
 * status as a fact instead of offering a choice. The chain is advanced afterwards
 * through AdminGovFeeDisbursementStatusDialog.
 *
 * `renewalTaskId` IS OPTIONAL FOR THIS ENTITY, and the distinction matters.
 * CreateGovFeeDisbursementCommandValidator places NO rule on it at all — it is a
 * plain nullable Guid. The handler then, if and only if it IS present, checks that
 * the task exists and belongs to the chosen company. So an untied fee is a
 * legitimate record and the picker is honestly optional; a *mismatched* task is the
 * only failure, and that arrives as a 409 naming the company the task belongs to.
 *
 * THE TASK PICKER IS NOT FILTERED BY STATUS HERE, unlike the Service Fee form.
 * Nothing about a government fee depends on a renewal task's status — a filing fee
 * is paid when the filing happens — so this offers the full list and restricts
 * nothing. Filtering to `Updated` would be a rule this endpoint does not have.
 *
 * ------------------------------------------------------------------
 * WHY THE DIALOG CALLS THE FILTER-OPTIONS HOOK ITSELF.
 * ------------------------------------------------------------------
 * useAdminBillingFilterOptions is keyed on ONE company id, and the page's copy is
 * keyed on the page's company-filter value. A create dialog has its own company
 * picker, so reusing the page's list would offer the WRONG company's tasks whenever
 * the reader picks a company other than the one the list is filtered to — and the
 * server would reject every one of them with a 409. So the dialog passes its own
 * selection in, and the company gate works as designed.
 *
 * This costs no extra request in the common case: the query key is
 * ['admin','billing','filter-options','tasks', <companyId>], so if the page already
 * has that company loaded the dialog reads the same cache entry, and if the reader
 * uses the page's own company there is no request at all.
 *
 * ------------------------------------------------------------------
 * TWO FIELDS HAVE NO LENGTH LIMIT ANYWHERE, and none is invented.
 * ------------------------------------------------------------------
 * `GovernmentReference` and `Notes` carry no MaximumLength rule in
 * CreateGovFeeDisbursementCommandValidator — only FeeDescription does. So neither
 * gets a maxlength and a caption says so, rather than implying a ceiling the server
 * does not enforce. The retainer and payment forms do cap their equivalents, because
 * those validators do.
 */
const schema = z.object({
  clientCompanyId: z.string().min(1, 'Client company is required.'),
  renewalTaskId: z.string(),
  feeDescription: z
    .string()
    .min(1, 'Fee description is required.')
    .max(
      BILLING_FIELD_LIMITS.feeDescription,
      `Fee description must not exceed ${BILLING_FIELD_LIMITS.feeDescription} characters — the server rejects a longer value.`,
    ),
  governmentReference: z.string(),
  /**
   * The amount is held as a STRING and converted at submit. Two reasons, and both
   * matter:
   *   - `type="number"` hands React an empty string for a cleared field, which is
   *     falsy and would fail a numeric check; the emptiness test belongs here.
   *   - the value is sent as a NUMBER, because the record field is `decimal` and a
   *     JSON string does not bind to one — it would record a disbursement that bills
   *     nothing, silently.
   */
  amount: z
    .string()
    .min(1, 'Amount is required.')
    .refine((value) => Number.isFinite(Number(value)), {
      message: 'Amount must be a number.',
    })
    .refine((value) => Number(value) >= 0, {
      message: 'Amount must be greater than or equal to 0.',
    }),
  currency: z.string().min(1, 'Currency is required.'),
  notes: z.string(),
})

/**
 * Shared failure handling for the billing create forms, mirroring the local
 * equivalent in AdminStaffFormDialog.
 *
 * A 404 here means a company or task id no longer exists; a 409 means the task
 * belongs to another company. Both carry the handler's own message, which is more
 * specific than anything this layer could write, so it is kept. Field-level details
 * are mapped onto the matching inputs by name and only a failure with no field to
 * attach it to reaches the banner.
 *
 * The success and error TOASTS are not repeated here: they belong to
 * useAdminBillingMutations, which guarantees all ten billing mutations report
 * themselves exactly once rather than trusting each caller to remember.
 */
function handleSubmitError(error, setError, setErrorMessage, fallback) {
  const fieldErrors = apiFieldErrors(error)
  const failedFields = Object.entries(fieldErrors)

  if (failedFields.length > 0) {
    failedFields.forEach(([field, message]) => {
      setError(field, { type: 'server', message })
    })
    return
  }

  setErrorMessage(extractApiErrorMessage(error, fallback))
}

function AdminGovFeeDisbursementFormDialog({ open, onClose, onCreated }) {
  const titleId = useId()
  const panelRef = useFocusTrap({ isOpen: open, onClose })
  const [errorMessage, setErrorMessage] = useState(null)

  const createDisbursement = useCreateAdminGovFeeDisbursement()

  const {
    register,
    handleSubmit,
    reset,
    watch,
    setValue,
    setError,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: {
      clientCompanyId: '',
      renewalTaskId: '',
      feeDescription: '',
      governmentReference: '',
      amount: '',
      currency: '',
      notes: '',
    },
  })

  const selectedCompanyId = watch('clientCompanyId')
  const amountValue = watch('amount')

  /**
   * The dialog's OWN company-scoped option lists, keyed on the company chosen HERE
   * rather than on the page's filter value. See the file header for why.
   */
  const {
    companyOptions,
    taskOptions,
    tasksEnabled,
    isTasksLoading,
    isTasksError,
  } = useAdminBillingFilterOptions(selectedCompanyId)

  // A reopened dialog must not carry the previous submission's values or its error.
  useEffect(() => {
    if (!open) return
    reset()
    setErrorMessage(null)
  }, [open, reset])

  /**
   * CHANGING COMPANY CLEARS THE TASK, because a task id chosen under the previous
   * company is no longer a valid pairing. Leaving it selected would post a company
   * and a task that disagree, which the handler refuses with a 409 — a request
   * guaranteed to fail for a reason the reader could not see. Clearing the field is
   * the honest response: the task list is rebuilt for the new company anyway.
   *
   * Declarative rather than an onChange handler, because the value can also change
   * through `reset()` and this must not leave a stale task behind on any of those
   * paths. It is skipped on the very first run, where there is nothing to clear and
   * `reset()` has already emptied the field.
   */
  const isFirstCompanyRun = useRef(true)
  useEffect(() => {
    if (isFirstCompanyRun.current) {
      isFirstCompanyRun.current = false
      return
    }
    setValue('renewalTaskId', '', { shouldValidate: false })
    setErrorMessage(null)
  }, [selectedCompanyId, setValue])

  /**
   * ZERO IS ADMITTED, NOT BLOCKED.
   *
   * The validator is `Amount >= 0` and its own message is "Amount must be greater
   * than or equal to 0." — so zero is legal and the schema enforces exactly that,
   * with no invented minimum above it. What is added is a warning, because a
   * zero-valued disbursement records money that was never spent while looking like a
   * real payment. The submit button stays ENABLED: blocking it would be the frontend
   * enforcing a rule the server does not have.
   */
  const isZeroAmount = amountValue !== '' && Number(amountValue) === 0
  const isPending = createDisbursement.isPending

  function onSubmit(values) {
    setErrorMessage(null)

    createDisbursement.mutate(
      {
        clientCompanyId: values.clientCompanyId,
        // "" is the form's way of saying "not chosen"; null is what the nullable
        // Guid binds. Sending "" would not bind.
        renewalTaskId: values.renewalTaskId ? values.renewalTaskId : null,
        feeDescription: values.feeDescription.trim(),
        governmentReference: presentText(values.governmentReference),
        amount: Number(values.amount),
        currency: values.currency.trim(),
        notes: presentText(values.notes),
      },
      {
        onSuccess: () => {
          onCreated?.()
          onClose()
        },
        onError: (error) =>
          handleSubmitError(
            error,
            setError,
            setErrorMessage,
            'Could not create this government fee disbursement.',
          ),
      },
    )
  }

  if (!open) {
    return null
  }

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto">
      <div
        className="fixed inset-0 bg-slate-900/40"
        onClick={isPending ? undefined : onClose}
        aria-hidden="true"
      />

      <div className="relative flex min-h-full items-end justify-center p-0 sm:items-center sm:p-6">
        <div
          ref={panelRef}
          role="dialog"
          aria-modal="true"
          aria-labelledby={titleId}
          tabIndex={-1}
          className="relative w-full max-w-lg rounded-t-[14px] border border-[#E2E4E9] bg-white p-5 shadow-[0_8px_24px_rgba(28,31,38,0.10)] focus:outline-none sm:rounded-[14px] sm:p-6"
        >
          <h2
            id={titleId}
            className="text-base font-semibold tracking-tight text-[#16181D]"
          >
            Record a government fee
          </h2>

          <p className="mt-1 text-xs leading-relaxed text-[#6B7280]">
            A disbursement records a government fee the firm has paid and is waiting
            to be reimbursed. It is not an invoice, so it carries no invoice number
            or due date, and the server sets its status to Paid by Firm on creation.
          </p>

          {isZeroAmount && (
            <div
              role="note"
              className="mt-4 flex items-start gap-2.5 rounded-[10px] border border-amber-200 bg-amber-50/60 px-3.5 py-3"
            >
              <AlertTriangle
                size={15}
                strokeWidth={2}
                className="mt-0.5 shrink-0 text-[#D97706]"
                aria-hidden="true"
              />
              <p className="text-xs text-[#92400E]">{ZERO_AMOUNT_WARNING}</p>
            </div>
          )}

          {errorMessage && (
            <div
              role="alert"
              className="mt-4 flex items-center gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-4 py-2.5"
            >
              <AlertCircle
                size={14}
                strokeWidth={2}
                className="shrink-0 text-[#DC2626]"
                aria-hidden="true"
              />
              <p className="text-xs font-medium text-[#DC2626]">{errorMessage}</p>
            </div>
          )}

          <form onSubmit={handleSubmit(onSubmit)} noValidate className="mt-5 flex flex-col gap-4">
            <FormField
              label="Client company"
              htmlFor="adminGovFeeClientCompany"
              error={errors.clientCompanyId?.message}
            >
              <select
                id="adminGovFeeClientCompany"
                disabled={isPending}
                className={errors.clientCompanyId ? inputErrorClass : inputClass}
                {...register('clientCompanyId')}
              >
                <option value="">Choose a company…</option>
                {companyOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </FormField>

            <FormField
              label="Renewal task (optional)"
              htmlFor="adminGovFeeRenewalTask"
              error={errors.renewalTaskId?.message}
            >
              <select
                id="adminGovFeeRenewalTask"
                disabled={isPending || !tasksEnabled}
                className={errors.renewalTaskId ? inputErrorClass : inputClass}
                {...register('renewalTaskId')}
              >
                <option value="">
                  {tasksEnabled ? 'Not tied to a task' : 'Choose a company first'}
                </option>
                {taskOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                    {option.status ? ` · ${option.status}` : ''}
                  </option>
                ))}
              </select>
            </FormField>

            {/* The task list is company-scoped, so its availability is reported
                honestly: unavailable, still loading, or failed are three different
                things and none of them means "this company has no tasks". */}
            {tasksEnabled && isTasksLoading && (
              <p className="-mt-2 text-xs text-[#6B7280]">Loading this company&apos;s tasks…</p>
            )}

            {isTasksError && (
              <p className="-mt-2 text-xs text-[#92400E]">
                This company&apos;s tasks could not be loaded, so the list may be
                incomplete. The task is optional, so you can still record this
                disbursement without one.
              </p>
            )}

            {tasksEnabled && !isTasksLoading && !isTasksError && taskOptions.length === 0 && (
              <p className="-mt-2 text-xs text-[#6B7280]">
                No renewal tasks are listed for this company. The list has no search
                parameter, so a task beyond its first page cannot be shown here. The
                task is optional, so this is not a blocker.
              </p>
            )}

            <FormField
              label="Fee description"
              htmlFor="adminGovFeeDescription"
              error={errors.feeDescription?.message}
            >
              <input
                id="adminGovFeeDescription"
                type="text"
                disabled={isPending}
                className={errors.feeDescription ? inputErrorClass : inputClass}
                {...register('feeDescription')}
              />
            </FormField>

            <FormField
              label="Amount"
              htmlFor="adminGovFeeAmount"
              error={errors.amount?.message}
            >
              <input
                id="adminGovFeeAmount"
                type="number"
                step="0.01"
                inputMode="decimal"
                disabled={isPending}
                className={errors.amount ? inputErrorClass : inputClass}
                {...register('amount')}
              />
            </FormField>

            <p className="-mt-2 text-xs text-[#6B7280]">{AMOUNT_RULE_NOTE}</p>

            <FormField
              label="Currency"
              htmlFor="adminGovFeeCurrency"
              error={errors.currency?.message}
            >
              <input
                id="adminGovFeeCurrency"
                type="text"
                disabled={isPending}
                className={errors.currency ? inputErrorClass : inputClass}
                {...register('currency')}
              />
            </FormField>

            <p className="-mt-2 text-xs text-[#6B7280]">{CURRENCY_FIELD_NOTE}</p>

            <FormField
              label="Government reference (optional)"
              htmlFor="adminGovFeeReference"
              error={errors.governmentReference?.message}
            >
              <input
                id="adminGovFeeReference"
                type="text"
                disabled={isPending}
                className={errors.governmentReference ? inputErrorClass : inputClass}
                {...register('governmentReference')}
              />
            </FormField>

            <FormField
              label="Notes (optional)"
              htmlFor="adminGovFeeNotes"
              error={errors.notes?.message}
            >
              <textarea
                id="adminGovFeeNotes"
                rows={3}
                disabled={isPending}
                className={`${errors.notes ? inputErrorClass : inputClass} resize-y`}
                {...register('notes')}
              />
            </FormField>

            <p className="-mt-2 text-xs text-[#6B7280]">
              The government reference and these notes have no length limit on the
              server — it validates neither — so nothing here truncates them.
            </p>

            <div className="flex flex-col-reverse gap-2 pt-1 sm:flex-row sm:justify-end">
              <button
                type="button"
                onClick={onClose}
                disabled={isPending}
                className="inline-flex cursor-pointer items-center justify-center rounded-[10px] border border-[#E2E4E9] bg-white px-4 py-2.5 text-sm font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={isPending}
                className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {isPending ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                    <span>Saving…</span>
                  </>
                ) : (
                  <span>Record disbursement</span>
                )}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  )
}

export default AdminGovFeeDisbursementFormDialog
