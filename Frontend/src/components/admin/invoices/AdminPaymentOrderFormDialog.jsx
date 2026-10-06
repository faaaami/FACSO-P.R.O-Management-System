import { useEffect, useId, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, Loader2 } from 'lucide-react'
import ConfirmDialog from '../../shared/ConfirmDialog'
import FormField, { inputClass, inputErrorClass } from '../../client/settings/FormField'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import { useAdminBillingFilterOptions } from '../../../hooks/admin/useAdminBillingFilterOptions'
import { useCreateAdminPaymentOrder } from '../../../hooks/admin/useAdminBillingMutations'
import {
  getAdminRetainerInvoices,
  getAdminServiceFeeInvoices,
} from '../../../api/admin/invoices'
import { apiFieldErrors } from '../../../utils/apiFieldErrors'
import { extractApiErrorMessage } from '../../../utils/apiError'
import {
  BILLING_ACTION_CONFIRMS,
  BILLING_FIELD_LIMITS,
  IRREVERSIBLE_ACTIONS,
  PAYMENT_INVOICE_LABEL_NOTE,
  PAYMENT_MODEL_NOTE,
  PAYMENT_NO_CAPTURE_NOTE,
  PAYMENT_ONE_INVOICE_ONLY_NOTE,
  PAYMENT_PENDING_ONLY_NOTE,
  moneyParts,
  paymentInvoiceTypeText,
  presentText,
} from './billingDisplay'

/**
 * Raise a payment order against a pending invoice — POST /api/v1/payment-orders,
 * behind a confirmation, because raising one is a one-way act.
 *
 * ------------------------------------------------------------------
 * THE REQUEST IS EXACTLY FOUR FIELDS AND THE FORM SENDS EXACTLY FOUR.
 * ------------------------------------------------------------------
 * CreatePaymentOrderRequest is (InvoiceType, RetainerInvoiceId,
 * ServiceFeeInvoiceId, Notes). There is NO amount, NO currency and NO
 * clientCompanyId, because CreatePaymentOrderCommandHandler copies Amount, Currency
 * and ClientCompanyId off the referenced invoice itself. A form with an amount field
 * would be a control whose value the server ignores.
 *
 * THE COMPANY PICKER IS NOT SENT EITHER — it exists only to narrow the invoice
 * list, and the id posted is the invoice's own. That is why the payload is built
 * from `selectedInvoiceId` and the chosen type rather than from the form's company
 * field.
 *
 * EXACTLY ONE ID COLUMN IS FILLED, and it is the one matching the chosen type: the
 * handler rejects a request naming the other kind. The wrapper sends the other
 * column as null, so the payload can never present both.
 *
 * ------------------------------------------------------------------
 * THE TYPE IS SENT AS ITS ENUM, AND IS THE ONLY NUMERIC FIELD IN THE FORM.
 * ------------------------------------------------------------------
 * `PaymentInvoiceType` is RetainerInvoice = 1, ServiceFeeInvoice = 2, and
 * Program.cs's global JsonStringEnumConverter serialises the NAME. So the form holds
 * the numeric ordinal and the API wrapper turns it into the name — which also keeps
 * this file on the same numeric vocabulary the payment READ DTOs use, since those
 * declare `int InvoiceType` and arrive as numbers.
 *
 * ------------------------------------------------------------------
 * WHY THE PENDING INVOICES ARE FETCHED HERE AND NOT THROUGH useAdminInvoices.
 * ------------------------------------------------------------------
 * The three list hooks have no `enabled` option: they call useQuery unconditionally,
 * so a dialog that used one would issue "all pending invoices, every company" the
 * moment it opened, before a company had been chosen. That request is both wasteful
 * and semantically wrong for a form that is about to post one specific invoice. The
 * hooks are in a completed file shared by the page, so instead this dialog queries
 * the API wrappers directly, with the gate it actually needs.
 *
 * The query keys are written to MATCH the list hooks' key shape exactly, so when the
 * reader happens to be on the Retainer tab filtered to Pending for the same company,
 * this dialog reads the page's existing cache entry rather than issuing a second,
 * identical request. Sharing the key format is deliberate; it is not a coincidence.
 *
 * Only PENDING invoices are offered, because that is the only status the handler
 * accepts. The filter is applied SERVER-side via `status`, and also defensively in the
 * client, so a stale cached row that has since been paid cannot be submitted.
 *
 * ------------------------------------------------------------------
 * TWO THINGS THIS FORM MUST NOT IMPLY.
 * ------------------------------------------------------------------
 * That it settles the invoice — it does not; the client pays through Razorpay and
 * the webhook settles it. And that it takes the money — nothing in this portal
 * captures a payment. Both are stated on the form rather than discovered later.
 */
const INVOICE_TYPE_RETAINER = 1
const INVOICE_TYPE_SERVICE_FEE = 2

/** A generous but bounded first page; a company's pending invoices are few. */
const INVOICE_PAGE_SIZE = 100

const schema = z.object({
  clientCompanyId: z.string().min(1, 'Client company is required.'),
  invoiceType: z.string().min(1, 'Invoice type is required.'),
  selectedInvoiceId: z.string().min(1, 'Select an invoice to pay.'),
  // The ONLY bounded field on this form: CreatePaymentOrderCommandValidator caps
  // Notes at 5000. Unlike the other billing creates, which validate no notes field
  // at all, so this one really is limited.
  notes: z
    .string()
    .max(
      BILLING_FIELD_LIMITS.paymentNotes,
      `Notes must not exceed ${BILLING_FIELD_LIMITS.paymentNotes} characters — the server rejects a longer value.`,
    ),
})

function handleSubmitError(error, setError, setErrorMessage) {
  const message = extractApiErrorMessage(error, '')
  const lower = message.toLowerCase()

  if (error?.response?.status === 409) {
    /*
     * THE TWO 409s THIS ENDPOINT CAN RETURN ARE ABOUT DIFFERENT THINGS, AND THEY ARE
     * NOT PUT IN THE SAME PLACE.
     *
     * "Only pending invoices can be paid" / not-found goes ON THE PICKER, because it is
     * a statement about the selection: the invoice chosen is no longer payable, so the
     * reader's next action is to choose a different one. Attaching it anywhere else
     * would leave a correct, payable invoice selected next to a message implying they
     * chose wrong.
     *
     * "A payment order already exists for this invoice" goes to the BANNER, not to the
     * notes field it once named. It is not a remark about the form's contents at all:
     * the invoice already has a gateway order, so the form is refusing to raise a
     * second. There is nothing here to correct, and the only way to clear it is to
     * cancel that order — a separate action, on a different screen, with its own
     * irreversible consequences, which this dialog has no business performing. So it
     * is reported as what it is: a state the reader must go and deal with elsewhere.
     */
    if (lower.includes('pending') || lower.includes('not found')) {
      setError('selectedInvoiceId', { type: 'server', message })
      return
    }
    if (lower.includes('already exists')) {
      setErrorMessage(message)
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

  setErrorMessage(message || 'Could not raise the payment order.')
}

function AdminPaymentOrderFormDialog({ open, onClose, onCreated }) {
  const titleId = useId()
  const panelRef = useFocusTrap({ isOpen: open, onClose })
  const [errorMessage, setErrorMessage] = useState(null)
  const [isConfirming, setIsConfirming] = useState(false)

  const createOrder = useCreateAdminPaymentOrder()

  const {
    register,
    handleSubmit,
    reset,
    watch,
    getValues,
    setValue,
    setError,
    formState: { errors, isValid },
  } = useForm({
    resolver: zodResolver(schema),
    mode: 'onChange',
    defaultValues: {
      clientCompanyId: '',
      invoiceType: String(INVOICE_TYPE_RETAINER),
      selectedInvoiceId: '',
      notes: '',
    },
  })

  const selectedCompanyId = watch('clientCompanyId')
  const invoiceType = Number(watch('invoiceType'))
  const selectedInvoiceId = watch('selectedInvoiceId')

  const { companyOptions } = useAdminBillingFilterOptions(selectedCompanyId)

  /**
   * The company gates both lists, and the type gates which one is used. Both queries
   * stay disabled until a company exists, so nothing is requested on open.
   *
   * The status filter is server-side, and the client re-checks it on the rows that
   * come back: the cache holds this list for 30s, so an invoice paid in another tab
   * can still be present here, and the handler would reject it with a 409 the reader
   * cannot act on. Filtering again means it is never offered.
   */
  const retainerInvoicesQuery = useQuery({
    queryKey: [
      'admin',
      'invoices',
      'retainer',
      {
        clientCompanyId: selectedCompanyId,
        serviceContractId: undefined,
        status: 'Pending',
        page: 1,
        pageSize: INVOICE_PAGE_SIZE,
      },
    ],
    queryFn: () =>
      getAdminRetainerInvoices({
        clientCompanyId: selectedCompanyId,
        status: 'Pending',
        page: 1,
        pageSize: INVOICE_PAGE_SIZE,
      }),
    enabled: Boolean(selectedCompanyId) && invoiceType === INVOICE_TYPE_RETAINER,
    staleTime: 30_000,
    retry: 1,
  })

  const serviceFeeInvoicesQuery = useQuery({
    queryKey: [
      'admin',
      'invoices',
      'service-fee',
      {
        clientCompanyId: selectedCompanyId,
        renewalTaskId: undefined,
        status: 'Pending',
        page: 1,
        pageSize: INVOICE_PAGE_SIZE,
      },
    ],
    queryFn: () =>
      getAdminServiceFeeInvoices({
        clientCompanyId: selectedCompanyId,
        status: 'Pending',
        page: 1,
        pageSize: INVOICE_PAGE_SIZE,
      }),
    enabled: Boolean(selectedCompanyId) && invoiceType === INVOICE_TYPE_SERVICE_FEE,
    staleTime: 30_000,
    retry: 1,
  })

  const activeQuery =
    invoiceType === INVOICE_TYPE_RETAINER ? retainerInvoicesQuery : serviceFeeInvoicesQuery

  const invoiceOptions = (activeQuery.data?.items ?? []).filter(
    (invoice) => invoice?.id && invoice?.status === 'Pending',
  )

  useEffect(() => {
    if (!open) return
    reset()
    setErrorMessage(null)
    setIsConfirming(false)
  }, [open, reset])

  /**
   * CHANGING COMPANY OR TYPE CLEARS THE CHOSEN INVOICE. An invoice id belongs to
   * exactly one company and one type, so keeping the old selection would post a pair
   * the server refuses: a wrong-company id is a 403/409 and a wrong-type id fails
   * the handler's type-to-column check. Either way it is a request that cannot
   * succeed, for a reason invisible to the reader.
   */
  const lastScope = useRef(null)
  useEffect(() => {
    if (!open) return
    const scope = `${selectedCompanyId}:${invoiceType}`
    if (lastScope.current === null) {
      lastScope.current = scope
      return
    }
    if (lastScope.current === scope) return
    lastScope.current = scope
    setValue('selectedInvoiceId', '', { shouldValidate: false })
    setErrorMessage(null)
  }, [selectedCompanyId, invoiceType, setValue, open])

  const selectedInvoice = invoiceOptions.find(
    (invoice) => invoice.id === selectedInvoiceId,
  )
  const selectedMoney = moneyParts(selectedInvoice?.amount, selectedInvoice?.currency)
  const isPending = createOrder.isPending
  const hasCompany = Boolean(selectedCompanyId)

  const confirmCopy = BILLING_ACTION_CONFIRMS[IRREVERSIBLE_ACTIONS.PAYMENT_CREATE]
  const typeLabel = paymentInvoiceTypeText(invoiceType) ?? 'invoice'

  /**
   * THE SUBMIT BUTTON DOES NOT RAISE THE ORDER. It opens the confirmation, and only
   * the confirmation's own confirm action calls the mutation — the approved decision
   * that raising a payment order is confirmed first. `isValid` is the gate for
   * opening it, so the confirmation can never describe an incomplete selection.
   */
  function openConfirmation() {
    setErrorMessage(null)
    setIsConfirming(true)
  }

  function confirmCreate() {
    setErrorMessage(null)

    createOrder.mutate(
      {
        // The numeric ordinal the wrapper converts to the enum NAME on the wire.
        invoiceType,
        // Exactly one column is populated; the wrapper sends the other as null.
        retainerInvoiceId:
          invoiceType === INVOICE_TYPE_RETAINER ? selectedInvoiceId : null,
        serviceFeeInvoiceId:
          invoiceType === INVOICE_TYPE_SERVICE_FEE ? selectedInvoiceId : null,
        notes: presentText(getValues('notes')),
      },
      {
        onSuccess: () => {
          onCreated?.()
          onClose()
        },
        onError: (error) => {
          setIsConfirming(false)
          handleSubmitError(error, setError, setErrorMessage)
        },
      },
    )
  }

  if (!open) {
    return null
  }

  const confirmationMessage = [
    selectedInvoice
      ? `This raises a payment order for ${presentText(selectedInvoice.invoiceNumber) ?? 'the selected invoice'}, ${selectedMoney.formatted}.`
      : null,
    `It is a ${typeLabel} order.`,
    PAYMENT_MODEL_NOTE,
    confirmCopy.irreversible,
  ]
    .filter(Boolean)
    .join(' ')

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
            Raise a payment order
          </h2>

          <p className="mt-1 text-xs leading-relaxed text-[#6B7280]">
            The amount and currency are taken from the invoice by the server, so they
            are not entered here. {PAYMENT_PENDING_ONLY_NOTE}
          </p>

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

          <form
            onSubmit={handleSubmit(() => openConfirmation())}
            noValidate
            className="mt-5 flex flex-col gap-4"
          >
            <FormField
              label="Invoice type"
              htmlFor="adminPaymentInvoiceType"
              error={errors.invoiceType?.message}
            >
              <select
                id="adminPaymentInvoiceType"
                disabled={isPending}
                className={errors.invoiceType ? inputErrorClass : inputClass}
                {...register('invoiceType')}
              >
                <option value={String(INVOICE_TYPE_RETAINER)}>Retainer invoice</option>
                <option value={String(INVOICE_TYPE_SERVICE_FEE)}>Service Fee invoice</option>
              </select>
            </FormField>

            <FormField
              label="Client company"
              htmlFor="adminPaymentClientCompany"
              error={errors.clientCompanyId?.message}
            >
              <select
                id="adminPaymentClientCompany"
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

            <div aria-live="polite">
              <FormField
                label="Pending invoice to pay"
                htmlFor="adminPaymentInvoice"
                error={errors.selectedInvoiceId?.message}
              >
                <select
                  id="adminPaymentInvoice"
                  disabled={isPending || !hasCompany}
                  className={errors.selectedInvoiceId ? inputErrorClass : inputClass}
                  {...register('selectedInvoiceId')}
                >
                  <option value="">
                    {hasCompany ? 'Choose an invoice…' : 'Choose a company first'}
                  </option>
                  {invoiceOptions.map((invoice) => {
                    const money = moneyParts(invoice.amount, invoice.currency)
                    return (
                      <option key={invoice.id} value={invoice.id}>
                        {presentText(invoice.invoiceNumber) ?? invoice.id.slice(0, 8)} —{' '}
                        {money.formatted}
                      </option>
                    )
                  })}
                </select>
              </FormField>
            </div>

            {hasCompany && activeQuery.isLoading && (
              <p className="-mt-2 text-xs text-[#6B7280]">
                Looking for this company&apos;s pending invoices…
              </p>
            )}

            {activeQuery.isError && (
              <p className="-mt-2 text-xs text-[#92400E]">
                Pending invoices could not be loaded, so none can be offered. The form
                stays disabled until that succeeds.
              </p>
            )}

            {hasCompany && !activeQuery.isLoading && !activeQuery.isError && invoiceOptions.length === 0 && (
              <p className="-mt-2 text-xs text-[#6B7280]">
                This company has no pending {typeLabel.toLowerCase()} invoices, and
                only a pending invoice can be paid. Paid and voided invoices are not
                offered.
              </p>
            )}

            {invoiceOptions.length > 0 && (
              <p className="-mt-2 text-xs text-[#6B7280]">{PAYMENT_INVOICE_LABEL_NOTE}</p>
            )}

            <FormField
              label={`Notes (optional, up to ${BILLING_FIELD_LIMITS.paymentNotes} characters)`}
              htmlFor="adminPaymentNotes"
              error={errors.notes?.message}
            >
              <textarea
                id="adminPaymentNotes"
                rows={3}
                disabled={isPending}
                className={`${errors.notes ? inputErrorClass : inputClass} resize-y`}
                {...register('notes')}
              />
            </FormField>

            <p className="-mt-2 text-xs leading-relaxed text-[#6B7280]">
              {PAYMENT_ONE_INVOICE_ONLY_NOTE} {PAYMENT_NO_CAPTURE_NOTE}
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
                disabled={isPending || !isValid}
                className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {isPending ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                    <span>Working…</span>
                  </>
                ) : (
                  <span>{confirmCopy.confirmLabel}</span>
                )}
              </button>
            </div>
          </form>
        </div>
      </div>

      <ConfirmDialog
        open={isConfirming}
        title={confirmCopy.title}
        message={confirmationMessage}
        confirmLabel={confirmCopy.confirmLabel}
        tone={confirmCopy.tone}
        isLoading={isPending}
        onConfirm={confirmCreate}
        onCancel={() => {
          if (isPending) return
          setIsConfirming(false)
        }}
      />
    </div>
  )
}

export default AdminPaymentOrderFormDialog
