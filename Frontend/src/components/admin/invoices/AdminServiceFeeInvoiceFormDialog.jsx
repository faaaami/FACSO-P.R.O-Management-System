import { useEffect, useId, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, AlertTriangle, Loader2 } from 'lucide-react'
import FormField, { inputClass, inputErrorClass } from '../../client/settings/FormField'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import {
  useAdminBillingFilterOptions,
  useAdminUpdatedTaskOptions,
} from '../../../hooks/admin/useAdminBillingFilterOptions'
import { useCreateAdminServiceFeeInvoice } from '../../../hooks/admin/useAdminBillingMutations'
import { apiFieldErrors } from '../../../utils/apiFieldErrors'
import { extractApiErrorMessage } from '../../../utils/apiError'
import {
  AMOUNT_RULE_NOTE,
  BILLING_FIELD_LIMITS,
  CURRENCY_FIELD_NOTE,
  INVOICE_NUMBER_LIMIT_NOTE,
  INVOICE_NUMBER_UNIQUE_NOTE,
  SERVICE_FEE_CREATE_BLOCKED_NOTE,
  SERVICE_FEE_CREATE_NEEDS_COMPANY_FIRST,
  SERVICE_FEE_CREATE_NO_TASKS_AT_ALL,
  SERVICE_FEE_CREATE_NO_UPDATED_TASKS,
  SERVICE_FEE_CREATE_SERVER_ONLY_RULES,
  ZERO_AMOUNT_WARNING,
  presentText,
} from './billingDisplay'

/**
 * Create dialog for a Service Fee invoice — POST /api/v1/service-fee-invoices.
 *
 * ------------------------------------------------------------------
 * THE FORM IS WIRED, AND IN THE NORMAL COMPLETION PATH THE SERVER WILL
 * ALREADY HAVE AN INVOICE FOR THAT TASK.
 * ------------------------------------------------------------------
 * ServiceFeeInvoiceCreationService refuses the request unless the renewal task's
 * status is `Updated`, and it also refuses if the task ALREADY has a non-deleted
 * invoice. Two facts about the current platform:
 *
 *   - `ServiceFeeAmount > 0` IS reachable. PATCH /api/v1/admin/tasks/{taskId}/
 *     service-fee assigns it, and AdminTaskServiceFeePanel drives it. An earlier
 *     version of this header claimed no workflow assigns that amount; that is no
 *     longer true.
 *   - Reaching `Updated` DOES happen, and completion itself creates the invoice.
 *     UpdateRenewalTaskStatusCommandHandler publishes RenewalTaskCompletedEvent
 *     inside the status transaction, and CreateServiceFeeInvoiceOnTaskCompletedHandler
 *     calls this same creation service in that same transaction.
 *
 * The consequence is that a task which reached `Updated` through the normal Agent
 * completion flow ALREADY has its service-fee invoice, so submitting this form
 * against it returns the server's "A service fee invoice already exists for this
 * renewal task." 409. Whether this Admin entry point should remain is an open
 * product decision (F-33) and is deliberately NOT resolved here — this dialog is
 * left exactly as it was, and this header only describes what the code does.
 *
 * So, accurately describing this component:
 *   - the form is built in full, because the capability is real;
 *   - the task picker offers ONLY `Updated` tasks, so the obvious mistake — choosing
 *     a task of the wrong status — cannot be made at all;
 *   - when no task qualifies, the submit button is DISABLED and the reason is shown,
 *     so the reader learns why instead of watching a request fail with a 409;
 *   - nothing fabricates a task, and nothing bypasses the rule. The gate is a
 *     disabled control, never a hidden request or a hardcoded failure.
 *
 * THE PICKER IS OFFERED UNFILTERED-ISH AND THE GATE IS DATA, NOT HARD-CODED, so if
 * a task ever does reach `Updated` the submission works with no code change.
 *
 * ------------------------------------------------------------------
 * TWO SERVER RULES REMAIN UNCHECKABLE FROM THE OPTION LIST, and are named as such.
 * ------------------------------------------------------------------
 * ServiceFeeInvoiceCreationService also enforces that the invoice number is unused
 * and that the task does not ALREADY have a non-deleted invoice. Neither is knowable
 * from RenewalTaskListItemDto, which carries no invoice reference. Pre-checking
 * them would need a per-task request and could still race, so they are left to the
 * server and surfaced with its own 409 message.
 *
 * ------------------------------------------------------------------
 * THE FIELD SET IS NOT THE RETAINER FIELD SET.
 * ------------------------------------------------------------------
 * CreateServiceFeeInvoiceRequest has NO periodStart and NO periodEnd, and its
 * optional narrative field is `Description`, not `Notes`. So this form has neither
 * period field, and the one textarea it does have is the description. A period field
 * here would be a control the server silently discards.
 *
 * `RenewalTaskId` is REQUIRED here (`.NotEmpty()`), unlike the gov fee form's
 * optional one — a Service Fee invoice is defined by the task it bills.
 */
const schema = z
  .object({
    clientCompanyId: z.string().min(1, 'Client company is required.'),
    renewalTaskId: z.string().min(1, 'Renewal task is required.'),
    invoiceNumber: z
      .string()
      .min(1, 'Invoice number is required.')
      .max(
        BILLING_FIELD_LIMITS.invoiceNumber,
        `Invoice number must not exceed ${BILLING_FIELD_LIMITS.invoiceNumber} characters — the server rejects a longer value.`,
      ),
    invoiceDate: z.string().min(1, 'Invoice date is required.'),
    dueDate: z.string().min(1, 'Due date is required.'),
    // String in the form, number on the wire: a cleared `type="number"` is an empty
    // string, and the record field is `decimal`, which a JSON string does not bind.
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
    // Optional, and with NO MaximumLength anywhere in the validator — so no cap and
    // a caption saying so.
    description: z.string(),
  })
  // `DueDate >= InvoiceDate`, mirroring the validator. Compared as raw ISO strings,
  // which are lexicographically ordered and therefore compare exactly.
  .superRefine((values, ctx) => {
    if (values.invoiceDate && values.dueDate && values.dueDate < values.invoiceDate) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['dueDate'],
        message: 'Due date cannot be earlier than invoice date.',
      })
    }
  })

/**
 * Failure handling, mirroring AdminStaffFormDialog's local equivalent.
 *
 * The message is EXTRACTED FIRST AND THEN TESTED, never read off the response body
 * directly: a ConflictException is wrapped by GlobalExceptionMiddleware as
 * `{ error: { message } }`, so the text lives at `data.error.message` and a lookup at
 * `data.message` would find nothing.
 *
 * Three of this endpoint's 409s are about a FIELD on the form and are attached to
 * it, because that is where a reader looks for them:
 *   - the invoice number is taken          -> invoiceNumber
 *   - the task is already invoiced         -> renewalTaskId
 *   - the task is not `Updated`            -> renewalTaskId
 * The remaining one, a task belonging to another company, is about state rather than
 * an input and goes to the banner.
 */
function handleSubmitError(error, setError, setErrorMessage) {
  const message = extractApiErrorMessage(error, '')
  const lower = message.toLowerCase()

  if (error?.response?.status === 409) {
    if (lower.includes('invoice number')) {
      setError('invoiceNumber', { type: 'server', message })
      return
    }
    if (lower.includes('already exists for this renewal task') || lower.includes('must be completed')) {
      setError('renewalTaskId', { type: 'server', message })
      return
    }
  }

  const fieldErrors = apiFieldErrors(error)
  const failedFields = Object.entries(fieldErrors)

  if (failedFields.length > 0) {
    failedFields.forEach(([field, fieldMessage]) => {
      setError(field, { type: 'server', message: fieldMessage })
    })
    return
  }

  setErrorMessage(message || 'Could not create this Service Fee invoice.')
}

function AdminServiceFeeInvoiceFormDialog({ open, onClose, onCreated }) {
  const titleId = useId()
  const panelRef = useFocusTrap({ isOpen: open, onClose })
  const [errorMessage, setErrorMessage] = useState(null)

  const createInvoice = useCreateAdminServiceFeeInvoice()

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
      invoiceNumber: '',
      dueDate: '',
      amount: '',
      currency: '',
      description: '',
    },
  })

  const selectedCompanyId = watch('clientCompanyId')
  const amountValue = watch('amount')

  /**
   * The dialog's OWN company-scoped options, keyed on the company chosen HERE rather
   * than the page's filter value — reusing the page's list would offer another
   * company's tasks, and the server refuses every one of them.
   *
   * TWO QUERIES, BECAUSE THE DIALOG NEEDS TWO DIFFERENT THINGS.
   *
   * `companyOptions` and `tasksTotalCount` come from useAdminBillingFilterOptions, under
   * the key ['admin','billing','filter-options','tasks', <companyId>]. That entry is the
   * same one the page holds, so it costs no extra request when the companies match, and
   * `tasksTotalCount` is the company's true task count rather than the size of a capped
   * page — which is what lets the empty states below tell "has no tasks" apart from
   * "has tasks, none of them Updated".
   *
   * `updatedTaskOptions` comes from its own hook, because a Service Fee invoice can only
   * be raised against an `Updated` task and GET /admin/tasks DOES accept `?status=`.
   * Narrowing the shared list instead would have meant filtering one capped page of mixed
   * statuses, where a qualifying task past the cap is unreachable and the reader cannot
   * tell that from having none. That hook's comment covers the rest.
   */
  const { companyOptions, tasksTotalCount } =
    useAdminBillingFilterOptions(selectedCompanyId)

  const {
    updatedTaskOptions,
    updatedTasksEnabled,
    isUpdatedTasksLoading,
    isUpdatedTasksError,
  } = useAdminUpdatedTaskOptions(selectedCompanyId)

  const today = new Date().toISOString().slice(0, 10)

  useEffect(() => {
    if (!open) return
    reset({
      clientCompanyId: '',
      renewalTaskId: '',
      invoiceNumber: '',
      // Seeded with today's UTC date, matching the server's own DateTime.UtcNow. Safe
      // to default because the field is required, so an empty form blocks submission
      // rather than recording a wrong date. Due date is left empty: a payment term is
      // a business decision and guessing one would be inventing data.
      invoiceDate: today,
      dueDate: '',
      amount: '',
      currency: '',
      description: '',
    })
    setErrorMessage(null)
  }, [open, reset, today])

  /**
   * CHANGING COMPANY CLEARS THE TASK. A task id belongs to one company, so keeping
   * the previous selection would post a mismatched pair that the handler rejects with
   * a 409 — a request engineered to fail for a reason the reader cannot see. It is
   * also the only way the new company's `Updated` tasks replace the old ones in a
   * field that has already been validated once.
   */
  const isFirstCompanyRun = useRef(true)
  useEffect(() => {
    if (isFirstCompanyRun.current) {
      isFirstCompanyRun.current = false
      return
    }
    setValue('renewalTaskId', '', { shouldValidate: false })
    setValue('invoiceNumber', '', { shouldValidate: false })
    setErrorMessage(null)
  }, [selectedCompanyId, setValue])

  const hasCompany = Boolean(selectedCompanyId)
  const hasQualifyingTask = updatedTaskOptions.length > 0
  const isZeroAmount = amountValue !== '' && Number(amountValue) === 0
  const isPending = createInvoice.isPending

  /**
   * THE GATE. Submit needs a company AND at least one qualifying task, because the
   * server refuses the request without one. `isUpdatedTasksLoading` is deliberately NOT
   * treated as blocking-then-enabling on its own: until the list resolves, whether a
   * task qualifies is genuinely unknown, so the button stays disabled rather than
   * enabling optimistically for a request that is certain to fail. It is the UPDATED
   * query's flag, not the page's task list, because this is the one whose answer decides
   * the gate.
   */
  const cannotSubmit = !hasCompany || !hasQualifyingTask || isUpdatedTasksLoading

  function onSubmit(values) {
    setErrorMessage(null)

    createInvoice.mutate(
      {
        clientCompanyId: values.clientCompanyId,
        renewalTaskId: values.renewalTaskId,
        invoiceNumber: values.invoiceNumber.trim(),
        invoiceDate: values.invoiceDate,
        dueDate: values.dueDate,
        amount: Number(values.amount),
        currency: values.currency.trim(),
        // The field is named `description` on the wire, not `notes` — this DTO has no
        // Notes property at all.
        description: presentText(values.description),
      },
      {
        onSuccess: () => {
          onCreated?.()
          onClose()
        },
        onError: (error) => handleSubmitError(error, setError, setErrorMessage),
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
            Create a Service Fee invoice
          </h2>

          <p className="mt-1 text-xs leading-relaxed text-[#6B7280]">
            A Service Fee invoice bills a completed renewal task. It is created as
            Pending and stays that way until it is marked paid or voided.
          </p>

          {/*
            THE PREREQUISITE IS SHOWN BEFORE ANY FIELD, because it decides whether
            this form can do anything. Burying it under the fields would let a reader
            fill the whole form before discovering the submission is impossible.
          */}
          {hasCompany && !isUpdatedTasksLoading && !hasQualifyingTask && (
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
              <p className="text-xs leading-relaxed text-[#92400E]">
                {SERVICE_FEE_CREATE_BLOCKED_NOTE}
              </p>
            </div>
          )}

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
              htmlFor="adminServiceFeeClientCompany"
              error={errors.clientCompanyId?.message}
            >
              <select
                id="adminServiceFeeClientCompany"
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

            {/*
              The picker is `aria-live` because the option set changes asynchronously
              once a company is chosen. Without it, a screen reader user would be
              told nothing while the select's contents and the submit button's state
              both changed underneath them.
            */}
            <div aria-live="polite">
              <FormField
                label="Renewal task (must be Updated)"
                htmlFor="adminServiceFeeTask"
                error={errors.renewalTaskId?.message}
              >
                <select
                  id="adminServiceFeeTask"
                  disabled={isPending || !updatedTasksEnabled}
                  className={errors.renewalTaskId ? inputErrorClass : inputClass}
                  {...register('renewalTaskId')}
                >
                  <option value="">
                    {updatedTasksEnabled ? 'Choose a task…' : 'Choose a company first'}
                  </option>
                  {updatedTaskOptions.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </FormField>
            </div>

            {updatedTasksEnabled && isUpdatedTasksLoading && (
              <p className="-mt-2 text-xs text-[#6B7280]">
                Looking for this company&apos;s Updated tasks…
              </p>
            )}

            {isUpdatedTasksError && (
              <p className="-mt-2 text-xs text-[#92400E]">
                This company&apos;s tasks could not be loaded, so a qualifying task
                cannot be confirmed and the form stays disabled.
              </p>
            )}

            {updatedTasksEnabled &&
              !isUpdatedTasksLoading &&
              !isUpdatedTasksError &&
              !hasQualifyingTask &&
              tasksTotalCount === 0 && (
                <p className="-mt-2 text-xs text-[#6B7280]">
                  {SERVICE_FEE_CREATE_NO_TASKS_AT_ALL}
                </p>
              )}

            {updatedTasksEnabled &&
              !isUpdatedTasksLoading &&
              !isUpdatedTasksError &&
              !hasQualifyingTask &&
              tasksTotalCount > 0 && (
                <>
                  <p className="-mt-2 text-xs text-[#6B7280]">
                    {SERVICE_FEE_CREATE_NO_UPDATED_TASKS}
                  </p>
                  {/*
                    The two empty states above are told apart by the company's true task
                    count rather than by the length of a capped page, so this line can
                    state a real total instead of hedging about a page. The server has
                    already confirmed none of these are `Updated`, because the picker was
                    fetched with that filter rather than narrowed from this list.
                  */}
                  <p className="-mt-2 text-xs text-[#6B7280]">
                    This company has {tasksTotalCount}{' '}
                    {tasksTotalCount === 1 ? 'renewal task' : 'renewal tasks'}, and none
                    of {tasksTotalCount === 1 ? 'it is' : 'them are'} Updated. The server
                    accepts only an Updated task.
                  </p>
                </>
              )}

            {!hasCompany && (
              <p className="-mt-2 text-xs text-[#6B7280]">
                {SERVICE_FEE_CREATE_NEEDS_COMPANY_FIRST}
              </p>
            )}

            <FormField
              label="Invoice number"
              htmlFor="adminServiceFeeInvoiceNumber"
              error={errors.invoiceNumber?.message}
            >
              <input
                id="adminServiceFeeInvoiceNumber"
                type="text"
                disabled={isPending}
                className={errors.invoiceNumber ? inputErrorClass : inputClass}
                {...register('invoiceNumber')}
              />
            </FormField>

            <p className="-mt-2 text-xs text-[#6B7280]">
              {INVOICE_NUMBER_LIMIT_NOTE} {INVOICE_NUMBER_UNIQUE_NOTE}
            </p>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <FormField
                label="Invoice date"
                htmlFor="adminServiceFeeInvoiceDate"
                error={errors.invoiceDate?.message}
              >
                <input
                  id="adminServiceFeeInvoiceDate"
                  type="date"
                  disabled={isPending}
                  className={errors.invoiceDate ? inputErrorClass : inputClass}
                  {...register('invoiceDate')}
                />
              </FormField>

              <FormField
                label="Due date"
                htmlFor="adminServiceFeeDueDate"
                error={errors.dueDate?.message}
              >
                <input
                  id="adminServiceFeeDueDate"
                  type="date"
                  disabled={isPending}
                  className={errors.dueDate ? inputErrorClass : inputClass}
                  {...register('dueDate')}
                />
              </FormField>
            </div>

            <p className="-mt-2 text-xs text-[#6B7280]">
              The due date cannot be earlier than the invoice date. This invoice type
              has no billing period fields — the server&apos;s request record does not
              carry any.
            </p>

            <FormField
              label="Amount"
              htmlFor="adminServiceFeeAmount"
              error={errors.amount?.message}
            >
              <input
                id="adminServiceFeeAmount"
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
              htmlFor="adminServiceFeeCurrency"
              error={errors.currency?.message}
            >
              <input
                id="adminServiceFeeCurrency"
                type="text"
                disabled={isPending}
                className={errors.currency ? inputErrorClass : inputClass}
                {...register('currency')}
              />
            </FormField>

            <p className="-mt-2 text-xs text-[#6B7280]">{CURRENCY_FIELD_NOTE}</p>

            <FormField
              label="Description (optional)"
              htmlFor="adminServiceFeeDescription"
              error={errors.description?.message}
            >
              <textarea
                id="adminServiceFeeDescription"
                rows={3}
                disabled={isPending}
                className={`${errors.description ? inputErrorClass : inputClass} resize-y`}
                {...register('description')}
              />
            </FormField>

            <p className="-mt-2 text-xs text-[#6B7280]">
              This description has no length limit on the server — it validates none
              on this field — so nothing here truncates it.{' '}
              {SERVICE_FEE_CREATE_SERVER_ONLY_RULES}
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
                disabled={isPending || cannotSubmit}
                className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {isPending ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                    <span>Saving…</span>
                  </>
                ) : (
                  <span>Create invoice</span>
                )}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  )
}

export default AdminServiceFeeInvoiceFormDialog
