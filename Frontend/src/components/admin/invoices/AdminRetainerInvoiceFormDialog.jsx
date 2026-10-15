import { useEffect, useId, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, AlertTriangle, Info, Loader2 } from 'lucide-react'
import FormField, { inputClass, inputErrorClass } from '../../client/settings/FormField'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import { useAdminBillingFilterOptions } from '../../../hooks/admin/useAdminBillingFilterOptions'
import { useAdminActiveContract } from '../../../hooks/admin/useAdminContracts'
import { useCreateAdminRetainerInvoice } from '../../../hooks/admin/useAdminBillingMutations'
import { apiFieldErrors } from '../../../utils/apiFieldErrors'
import { extractApiErrorMessage } from '../../../utils/apiError'
import {
  AMOUNT_RULE_NOTE,
  BILLING_FIELD_LIMITS,
  CURRENCY_FIELD_NOTE,
  INVOICE_NUMBER_LIMIT_NOTE,
  INVOICE_NUMBER_UNIQUE_NOTE,
  RETAINER_ACTIVE_CONTRACT_CAVEAT,
  RETAINER_CONTRACT_IS_RESOLVED_NOTE,
  RETAINER_NO_ACTIVE_CONTRACT_NOTE,
  ZERO_AMOUNT_WARNING,
  formatDate,
  moneyParts,
  presentText,
} from './billingDisplay'

/**
 * Create dialog for a retainer invoice — POST /api/v1/retainer-invoices.
 *
 * ------------------------------------------------------------------
 * THE CONTRACT IS A READ-ONLY RESOLUTION, NOT A CHOICE.
 * ------------------------------------------------------------------
 * CreateRetainerInvoiceRequest does carry a `ServiceContractId`, so it looks
 * selectable. It is not: CreateRetainerInvoiceCommandHandler requires that
 * contract to belong to the posted company (409) AND to be Active (409), and there
 * is no request variant that relaxes either. A picker over the company's contracts
 * could therefore only ever produce a valid choice — the active one — and would
 * spend the reader's attention listing contracts the server will refuse.
 *
 * So the form resolves the company's active contract through
 * GET /clients/{id}/contracts/active and displays it. When the company has none, the
 * control explains why and submit stays disabled, rather than sending a request that
 * is certain to fail.
 *
 * AND THE RESOLUTION HAS A CAVEAT THE FORM SHOWS RATHER THAN HIDES: that endpoint
 * filters on Status alone and never compares dates, so a contract whose end date
 * has passed is still returned as active. The end date is printed beside it.
 *
 * ------------------------------------------------------------------
 * TWO DATE RULES ARE ENFORCED HERE AND BOTH MIRROR THE SERVER EXACTLY.
 * ------------------------------------------------------------------
 * CreateRetainerInvoiceCommandValidator states:
 *   - DueDate >= InvoiceDate, "Due date cannot be earlier than invoice date."
 *   - PeriodEnd >= PeriodStart, ONLY WHEN BOTH ARE PRESENT — it is `.When(x =>
 *     x.PeriodStart.HasValue && x.PeriodEnd.HasValue)`, so a lone period end is
 *     legal. That conditional matters: a form that always compared the two would
 *     invent a requirement the server does not have.
 *
 * Both comparisons are made on the raw "YYYY-MM-DD" strings. That is exact, not an
 * approximation: ISO-8601 dates are lexicographically ordered, so string comparison
 * and date comparison agree, and it avoids the timezone shift that parsing a
 * date-only string into a local Date and back can introduce.
 */
const EMPTY_PERIOD = { periodStart: '', periodEnd: '' }

const schema = z
  .object({
    clientCompanyId: z.string().min(1, 'Client company is required.'),
    invoiceNumber: z
      .string()
      .min(1, 'Invoice number is required.')
      .max(
        BILLING_FIELD_LIMITS.invoiceNumber,
        `Invoice number must not exceed ${BILLING_FIELD_LIMITS.invoiceNumber} characters — the server rejects a longer value.`,
      ),
    invoiceDate: z.string().min(1, 'Invoice date is required.'),
    dueDate: z.string().min(1, 'Due date is required.'),
    periodStart: z.string(),
    periodEnd: z.string(),
    /**
     * Held as a string and sent as a number, for the two reasons set out in the gov
     * fee form: a cleared `type="number"` yields an empty string, and the record
     * field is `decimal`, which a JSON string does not bind to.
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
    // No maximum. CreateRetainerInvoiceCommandValidator places NO rule on Notes —
    // unlike the payment-order validator, which caps its notes at 5000. So none is
    // invented here and the caption says the field is unbounded.
    notes: z.string(),
  })
  .superRefine((values, ctx) => {
    if (values.invoiceDate && values.dueDate && values.dueDate < values.invoiceDate) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['dueDate'],
        message: 'Due date cannot be earlier than invoice date.',
      })
    }

    // Gated on BOTH being present, matching the server's own `.When(...)`.
    if (values.periodStart && values.periodEnd && values.periodEnd < values.periodStart) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['periodEnd'],
        message: 'Period end cannot be earlier than period start.',
      })
    }
  })

/**
 * Failure handling, mirroring AdminStaffFormDialog's local equivalent.
 *
 * A 409 on this endpoint is one of three specific things — the contract is not
 * active, it belongs to another company, or the invoice number is already taken —
 * and each carries the handler's own message, which names the exact rule broken.
 * The duplicate-number case is attached to the invoice number field, because that is
 * where a reader looks for it and it is the only one of the three about a field on
 * this form; the two contract failures are about the company's state rather than any
 * input, so they go to the banner.
 *
 * THE MESSAGE IS EXTRACTED FIRST AND THEN TESTED, rather than reaching into the
 * response body for it. A ConflictException raised in a handler is wrapped by
 * GlobalExceptionMiddleware as `{ error: { message } }`, so the text lives at
 * `data.error.message` and NOT at `data.message`; a direct lookup on the latter finds
 * nothing and the duplicate would silently fall through to the banner. Going
 * through extractApiErrorMessage also means this test keeps working across both
 * documented body shapes instead of only the one it was written against.
 */
function handleSubmitError(error, setError, setErrorMessage) {
  const message = extractApiErrorMessage(error, '')

  if (
    error?.response?.status === 409 &&
    message.toLowerCase().includes('invoice number')
  ) {
    setError('invoiceNumber', {
      type: 'server',
      message,
    })
    return
  }

  const fieldErrors = apiFieldErrors(error)
  const failedFields = Object.entries(fieldErrors)

  if (failedFields.length > 0) {
    failedFields.forEach(([field, fieldMessage]) => {
      setError(field, { type: 'server', message: fieldMessage })
    })
    return
  }

  setErrorMessage(message || 'Could not create this retainer invoice.')
}

function AdminRetainerInvoiceFormDialog({ open, onClose, onCreated }) {
  const titleId = useId()
  const panelRef = useFocusTrap({ isOpen: open, onClose })
  const [errorMessage, setErrorMessage] = useState(null)

  const createInvoice = useCreateAdminRetainerInvoice()

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
      invoiceNumber: '',
      dueDate: '',
      ...EMPTY_PERIOD,
      amount: '',
      currency: '',
      notes: '',
    },
  })

  const selectedCompanyId = watch('clientCompanyId')
  const amountValue = watch('amount')

  const { companyOptions } = useAdminBillingFilterOptions(selectedCompanyId)
  const {
    data: activeContract,
    isLoading: isContractLoading,
    isError: isContractError,
  } = useAdminActiveContract(selectedCompanyId)

  /**
   * INVOICE DATE DEFAULTS TO TODAY, IN UTC, and only that one date.
   *
   * The server stamps its own timestamps with DateTime.UtcNow, so UTC is the
   * matching clock and using local time would place the invoice on the wrong day for
   * readers east or west of it. Defaulting is safe here in a way it was not for the
   * staff form's `isApproved`: this field is required, so an empty form blocks
   * submission rather than quietly recording a wrong date, and "the invoice is
   * dated today" is a true statement rather than a fabricated one. Due date is left
   * EMPTY on purpose — a payment term is a business decision, and guessing one would
   * be inventing data.
   */
  const today = new Date().toISOString().slice(0, 10)

  // A reopened dialog starts clean, seeded only with the one honest default.
  useEffect(() => {
    if (!open) return
    reset({
      clientCompanyId: '',
      invoiceNumber: '',
      invoiceDate: today,
      dueDate: '',
      ...EMPTY_PERIOD,
      amount: '',
      currency: '',
      notes: '',
    })
    setErrorMessage(null)
  }, [open, reset, today])

  /**
   * CHANGING COMPANY INVALIDATES THE RESOLVED CONTRACT'S PRECONDITION, so the form
   * is not left asserting a contract belonging to the previous company. The contract
   * refetches for the new id on its own; clearing the stale one immediately stops
   * the summary describing another company's agreement for the moment in between.
   */
  const isFirstCompanyRun = useRef(true)
  useEffect(() => {
    if (isFirstCompanyRun.current) {
      isFirstCompanyRun.current = false
      return
    }
    setValue('invoiceNumber', '', { shouldValidate: false })
    setErrorMessage(null)
  }, [selectedCompanyId, setValue])

  /**
   * SUBMIT IS BLOCKED WITHOUT AN ACTIVE CONTRACT, because the server would reject
   * it. This is the one place a create form is disabled on something the reader did
   * not type, and it is a server rule rather than a preference: the handler raises
   * 409 "The service contract must be active to create a retainer invoice." Sending
   * it anyway would be a request engineered to fail.
   */
  const hasCompany = Boolean(selectedCompanyId)
  const isContractResolved = Boolean(activeContract?.id)
  const blockedWithoutContract = hasCompany && !isContractLoading && !isContractResolved
  const isZeroAmount = amountValue !== '' && Number(amountValue) === 0
  const isPending = createInvoice.isPending

  const contractMoney = moneyParts(activeContract?.retainerAmount, null)
  const contractStatus = presentText(activeContract?.status)

  function onSubmit(values) {
    setErrorMessage(null)

    createInvoice.mutate(
      {
        clientCompanyId: values.clientCompanyId,
        // NOT a form field. It is read from the resolved contract, so the value
        // posted is the one the server will validate against — there is no path
        // through this form by which a different contract id can be submitted.
        serviceContractId: activeContract?.id,
        invoiceNumber: values.invoiceNumber.trim(),
        invoiceDate: values.invoiceDate,
        dueDate: values.dueDate,
        // Sent explicitly, as null when blank, so the payload is a complete
        // picture of the request record.
        periodStart: values.periodStart ? values.periodStart : null,
        periodEnd: values.periodEnd ? values.periodEnd : null,
        amount: Number(values.amount),
        currency: values.currency.trim(),
        notes: presentText(values.notes),
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
            Create a retainer invoice
          </h2>

          <p className="mt-1 text-xs leading-relaxed text-[#6B7280]">
            The invoice is raised against the company&apos;s active service contract,
            which the server resolves. It is created as Pending and stays that way
            until it is marked paid or voided.
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
              htmlFor="adminRetainerClientCompany"
              error={errors.clientCompanyId?.message}
            >
              <select
                id="adminRetainerClientCompany"
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
              THE RESOLVED CONTRACT. A definition list rather than a disabled input,
              because a disabled control's value is not reliably read by assistive
              technology and this is a fact about the record, not an input. `aria-live`
              is on the wrapper because the contract arrives asynchronously: without
              it the summary would change silently while a screen reader user is
              waiting on the submit button to become available.
            */}
            <div
              aria-live="polite"
              className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 px-3.5 py-3"
            >
              <p className="text-xs font-semibold text-[#16181D]">
                Service contract
              </p>

              {!hasCompany && (
                <p className="mt-1.5 text-xs text-[#6B7280]">
                  Choose a company first. Its active contract is resolved by the
                  server, and a retainer invoice must be raised against it.
                </p>
              )}

              {hasCompany && isContractLoading && (
                <p className="mt-1.5 text-xs text-[#6B7280]">
                  Looking up this company&apos;s active contract…
                </p>
              )}

              {hasCompany && isContractError && (
                <p className="mt-1.5 text-xs text-[#92400E]">
                  This company&apos;s contract could not be looked up, so the invoice
                  cannot be raised until that succeeds. The form stays disabled.
                </p>
              )}

              {blockedWithoutContract && (
                <p className="mt-1.5 text-xs text-[#92400E]">
                  {RETAINER_NO_ACTIVE_CONTRACT_NOTE}
                </p>
              )}

              {isContractResolved && (
                <>
                  <dl className="mt-1.5 space-y-0.5 text-xs text-[#6B7280]">
                    <div className="flex flex-wrap gap-x-2">
                      <dt className="font-medium text-[#16181D]">
                        {presentText(activeContract.contractNumber) ?? 'Unnumbered contract'}
                      </dt>
                      {contractStatus && <dd>· {contractStatus}</dd>}
                    </div>
                    <div className="flex flex-wrap gap-x-2">
                      <dt>Retainer amount</dt>
                      <dd className="text-[#16181D]">{contractMoney.formatted}</dd>
                    </div>
                    <div className="flex flex-wrap gap-x-2">
                      <dt>Period</dt>
                      <dd>
                        {formatDate(activeContract.startDate) ?? '—'} to{' '}
                        {formatDate(activeContract.endDate) ?? 'open-ended'}
                      </dd>
                    </div>
                  </dl>

                  <p className="mt-2 text-[11px] leading-relaxed text-[#6B7280]">
                    {RETAINER_CONTRACT_IS_RESOLVED_NOTE}
                  </p>
                  <p className="mt-1 text-[11px] leading-relaxed text-[#6B7280]">
                    {RETAINER_ACTIVE_CONTRACT_CAVEAT}
                  </p>
                </>
              )}
            </div>

            <FormField
              label="Invoice number"
              htmlFor="adminRetainerInvoiceNumber"
              error={errors.invoiceNumber?.message}
            >
              <input
                id="adminRetainerInvoiceNumber"
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
                htmlFor="adminRetainerInvoiceDate"
                error={errors.invoiceDate?.message}
              >
                <input
                  id="adminRetainerInvoiceDate"
                  type="date"
                  disabled={isPending}
                  className={errors.invoiceDate ? inputErrorClass : inputClass}
                  {...register('invoiceDate')}
                />
              </FormField>

              <FormField
                label="Due date"
                htmlFor="adminRetainerDueDate"
                error={errors.dueDate?.message}
              >
                <input
                  id="adminRetainerDueDate"
                  type="date"
                  disabled={isPending}
                  className={errors.dueDate ? inputErrorClass : inputClass}
                  {...register('dueDate')}
                />
              </FormField>
            </div>

            <p className="-mt-2 text-xs text-[#6B7280]">
              The due date cannot be earlier than the invoice date. The period
              fields are optional, and the end date is only compared to the start
              when both are given.
            </p>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <FormField
                label="Period start (optional)"
                htmlFor="adminRetainerPeriodStart"
                error={errors.periodStart?.message}
              >
                <input
                  id="adminRetainerPeriodStart"
                  type="date"
                  disabled={isPending}
                  className={errors.periodStart ? inputErrorClass : inputClass}
                  {...register('periodStart')}
                />
              </FormField>

              <FormField
                label="Period end (optional)"
                htmlFor="adminRetainerPeriodEnd"
                error={errors.periodEnd?.message}
              >
                <input
                  id="adminRetainerPeriodEnd"
                  type="date"
                  disabled={isPending}
                  className={errors.periodEnd ? inputErrorClass : inputClass}
                  {...register('periodEnd')}
                />
              </FormField>
            </div>

            <FormField
              label="Amount"
              htmlFor="adminRetainerAmount"
              error={errors.amount?.message}
            >
              <input
                id="adminRetainerAmount"
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
              htmlFor="adminRetainerCurrency"
              error={errors.currency?.message}
            >
              <input
                id="adminRetainerCurrency"
                type="text"
                disabled={isPending}
                className={errors.currency ? inputErrorClass : inputClass}
                {...register('currency')}
              />
            </FormField>

            <p className="-mt-2 text-xs text-[#6B7280]">{CURRENCY_FIELD_NOTE}</p>

            <FormField
              label="Notes (optional)"
              htmlFor="adminRetainerNotes"
              error={errors.notes?.message}
            >
              <textarea
                id="adminRetainerNotes"
                rows={3}
                disabled={isPending}
                className={`${errors.notes ? inputErrorClass : inputClass} resize-y`}
                {...register('notes')}
              />
            </FormField>

            <p className="flex items-start gap-2 text-xs text-[#6B7280]">
              <Info size={13} strokeWidth={2} className="mt-0.5 shrink-0" aria-hidden="true" />
              <span>
                These notes have no length limit on the server — it applies none to
                this field — so nothing here truncates them.
              </span>
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
                disabled={isPending || !hasCompany || !isContractResolved}
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

export default AdminRetainerInvoiceFormDialog
