import { useEffect, useRef, useState } from 'react'
import { CheckCircle2, Clock, FileText, Landmark, Receipt, XCircle } from 'lucide-react'
import Drawer from '../documents/Drawer'
import LoadingState from '../LoadingState'
import ErrorState from '../ErrorState'
import EmptyState from '../EmptyState'
import StatusPill from '../StatusPill'
import DownloadPdfButton from './DownloadPdfButton'
import PaymentCheckout from './PaymentCheckout'
import { useRetainerInvoice } from '../../../hooks/client/useRetainerInvoice'
import { useServiceFeeInvoice } from '../../../hooks/client/useServiceFeeInvoice'
import {
  RETAINER_INVOICE_STATUS,
  SERVICE_FEE_INVOICE_STATUS,
  GOV_FEE_DISBURSEMENT_STATUS,
  enumLabel,
} from '../enumLabels'
import { formatMoney, formatDate, formatDateTime } from './format'

function invoiceStatusTone(status) {
  if (status === 'Paid') return 'success'
  if (status === 'Void') return 'neutral'
  return 'warning'
}

function govStatusTone(status) {
  if (status === 'Reimbursed') return 'success'
  if (status === 'InvoicedToClient') return 'warning'
  return 'neutral'
}

function MetaRow({ label, value }) {
  return (
    <div className="rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] p-3">
      <span className="block text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">{label}</span>
      <p className="mt-1 text-sm font-semibold text-[#16181D] break-words">{value ?? 'N/A'}</p>
    </div>
  )
}

function AmountRow({ label, amount, currency }) {
  return (
    <div className="flex items-baseline justify-between gap-3 rounded-[8px] border border-[#E2E4E9] bg-white p-3">
      <span className="text-xs font-medium text-[#6B7280]">{label}</span>
      <span className="text-lg font-bold tracking-tight text-[#16181D]">
        {formatMoney(amount, currency)}
      </span>
    </div>
  )
}

function InfoText({ label, value, mono }) {
  if (!value) return null
  return (
    <div className="flex items-start justify-between gap-3">
      <span className="shrink-0 text-xs text-[#6B7280]">{label}</span>
      <span className={`text-xs font-medium text-[#16181D] break-words text-right ${mono ? 'font-mono' : ''}`}>
        {value}
      </span>
    </div>
  )
}

// How long to keep waiting for the provider's webhook before telling the
// client to contact the firm. The webhook is normally seconds behind the
// gateway, so this is a safety net against an infinite spinner, not an
// expected wait.
const CONFIRM_TIMEOUT_MS = 120_000

/**
 * Banner shown after the gateway accepted a payment but the webhook has not
 * settled the invoice yet.
 *
 * This lives in the drawer, not in PaymentCheckout, because the checkout is
 * unmounted as soon as the invoice is no longer Pending - which is exactly
 * when a payment settles. Owning the confirming state here is what keeps the
 * client informed across that transition.
 */
function ConfirmingBanner({ timedOut }) {
  return (
    <div
      className="flex items-start gap-2 rounded-[8px] p-3 text-xs font-medium border border-amber-200 bg-amber-50 text-[#92400E]"
      role="status"
      aria-live="polite"
    >
      <Clock size={14} className="shrink-0 mt-0.5" aria-hidden="true" />
      <div>
        <p className="font-semibold">
          {timedOut ? 'Still confirming your payment' : 'Confirming your payment'}
        </p>
        <p className="mt-1 font-normal text-[#16181D]/70">
          {timedOut
            ? 'This is taking longer than usual. Your payment was submitted — please contact the PRO team if it does not update shortly. You do not need to pay again.'
            : 'Your payment was submitted and the provider is confirming it. This page updates automatically — you can close it and check back later.'}
        </p>
      </div>
    </div>
  )
}

/** Shown when the payment order was cancelled while the client was waiting. */
function CancelledPaymentBanner() {
  return (
    <div
      className="flex items-start gap-2 rounded-[8px] p-3 text-xs font-medium border border-red-200 bg-red-50 text-[#DC2626]"
      role="status"
      aria-live="polite"
    >
      <XCircle size={14} className="shrink-0 mt-0.5" aria-hidden="true" />
      <div>
        <p className="font-semibold">This payment was cancelled</p>
        <p className="mt-1 font-normal text-[#16181D]/70">
          The payment order was cancelled before it completed. Nothing was charged — you can
          start a new payment below.
        </p>
      </div>
    </div>
  )
}

function InvoiceDetailDrawer({ kind, id, item, onClose }) {
  // 'idle'    - no payment in flight
  // 'confirming' - gateway accepted, waiting for the webhook
  const [paymentState, setPaymentState] = useState('idle')
  const [timedOut, setTimedOut] = useState(false)
  const timeoutRef = useRef(null)

  const isGov = kind === 'gov'
  // Government fee disbursements are never self-service, so they never
  // confirm and never poll.
  const confirming = !isGov && paymentState === 'confirming'

  const retainer = useRetainerInvoice(
    kind === 'retainer' ? id : null,
    { poll: confirming && kind === 'retainer' },
  )
  const serviceFee = useServiceFeeInvoice(
    kind === 'serviceFee' ? id : null,
    { poll: confirming && kind === 'serviceFee' },
  )

  const active = kind === 'retainer' ? retainer : serviceFee
  const isLoading = !isGov && active.isLoading
  const isError = !isGov && active.isError
  const refetch = active.refetch
  const data = isGov ? item : active.data

  // Once a confirming payment reaches a terminal state, stop polling and drop
  // the banner: the invoice status itself becomes the source of truth.
  const orderStatus = data?.paymentOrderStatus
  useEffect(() => {
    if (paymentState !== 'confirming') return
    if (data?.status === 'Paid' || data?.status === 'Void') {
      setPaymentState('idle')
    } else if (orderStatus === 'Cancelled' || orderStatus === 5) {
      // Both wire forms: PaymentStatus is rendered as a name by the global
      // enum converter, but arrives numeric on any response that escapes it.
      setPaymentState('cancelled')
    }
  }, [paymentState, data?.status, orderStatus])

  // Safety net so a webhook that never arrives cannot leave a spinner up
  // forever. Cleared on every state change and on unmount.
  useEffect(() => {
    if (paymentState !== 'confirming') {
      setTimedOut(false)
      return undefined
    }

    timeoutRef.current = setTimeout(() => setTimedOut(true), CONFIRM_TIMEOUT_MS)

    return () => clearTimeout(timeoutRef.current)
  }, [paymentState])

  // Leaving the drawer with a payment still unconfirmed must not leave polling
  // running in the background.
  useEffect(() => () => clearTimeout(timeoutRef.current), [])

  const handlePaymentSubmitted = () => {
    setTimedOut(false)
    setPaymentState('confirming')
    // Nudge the detail query immediately instead of waiting a full poll
    // interval for the first read.
    refetch?.()
  }

  const drawerConfig = {
    retainer: {
      title: 'Retainer Invoice',
      subtitle: 'Retainer billing on your service contract',
      icon: Receipt,
      statusMap: RETAINER_INVOICE_STATUS,
      statusTone: invoiceStatusTone,
    },
    serviceFee: {
      title: 'Service Fee Invoice',
      subtitle: 'One-off service fee on a renewal task',
      icon: FileText,
      statusMap: SERVICE_FEE_INVOICE_STATUS,
      statusTone: invoiceStatusTone,
    },
    gov: {
      title: 'Government Fee',
      subtitle: 'Government fee disbursement on your account',
      icon: Landmark,
      statusMap: GOV_FEE_DISBURSEMENT_STATUS,
      statusTone: govStatusTone,
    },
  }[kind]

  const showCheckout = kind === 'retainer' || kind === 'serviceFee'
  const showPdf = showCheckout

  let content

  if (isLoading) {
    content = <LoadingState label="Loading details…" />
  } else if (isError) {
    content = (
      <ErrorState
        message={
          kind === 'gov'
            ? 'Could not load this disbursement. It may have been removed.'
            : 'Could not load this invoice. It may have been removed.'
        }
        onRetry={() => refetch()}
      />
    )
  } else if (!data) {
    content = <EmptyState icon={drawerConfig.icon} message="Details are not available." />
  } else {
    const item = data
    const statusLabel = enumLabel(drawerConfig.statusMap, item.status) ?? 'N/A'
    const statusTone = drawerConfig.statusTone(item.status)

    const settled = isGov
      ? item.status === 'Reimbursed'
        ? { label: 'Reimbursed', date: item.reimbursedAt }
        : null
      : item.status === 'Paid' || item.status === 'Reimbursed'
        ? { label: 'Settled', date: item.paidAt ?? item.reimbursedAt }
        : item.status === 'Void'
          ? { label: 'Voided', reason: item.voidReason }
          : null

    content = (
      <div className="space-y-5">
        {/* Identity */}
        <div className="flex flex-col gap-3 rounded-[10px] bg-[#F7F8FA] border border-[#E2E4E9] p-4">
          <div className="flex flex-wrap items-center gap-2">
            <StatusPill label={statusLabel} tone={statusTone} />
          </div>
          <p className="break-words text-xs font-medium text-[#6B7280] uppercase tracking-wider">
            {showCheckout ? (
              <>Invoice #{item.invoiceNumber ?? item.id}</>
            ) : (
              <>Disbursement #{item.id}</>
            )}
          </p>
        </div>

        {/* Settled / voided banner */}
        {settled && (
          <div
            className={`flex items-start gap-2 rounded-[8px] p-3 text-xs font-medium border ${
              item.status === 'Void'
                ? 'bg-gray-100 text-[#6B7280] border-[#E2E4E9]'
                : 'bg-[rgba(15,157,116,0.08)] text-[#0F9D74] border-[#0F9D74]/20'
            }`}
          >
            <CheckCircle2 size={14} className="shrink-0 mt-0.5" aria-hidden="true" />
            <div>
              <p className="font-semibold">
                {settled.label}
                {settled.date ? ` on ${formatDate(settled.date)}` : ''}
              </p>
              {settled.reason && <p className="mt-1 font-normal text-[#16181D]/70">{settled.reason}</p>}
            </div>
          </div>
        )}

        {/* Confirming / cancelled payment banners */}
        {paymentState === 'confirming' && <ConfirmingBanner timedOut={timedOut} />}
        {paymentState === 'cancelled' && <CancelledPaymentBanner />}

        {/* Total */}
        <AmountRow
          label={kind === 'gov' ? 'Government fee amount' : 'Invoice amount'}
          amount={item.amount}
          currency={item.currency}
        />

        {/* Entity context */}
        <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3 text-xs">
          <span className="font-medium text-[#6B7280]">Details</span>
          <dl className="mt-2 space-y-1.5">
            {item.contractNumber && <InfoText label="Contract #" value={item.contractNumber} mono />}
            {item.description && <InfoText label="Description" value={item.description} />}
            {kind === 'gov' && (
              <>
                <InfoText label="Fee" value={item.feeDescription} />
                <InfoText label="Gov reference" value={item.governmentReference} mono />
              </>
            )}
            <InfoText label="Currency" value={item.currency} />
          </dl>
        </div>

        {/* Key metadata */}
        <div className="grid grid-cols-2 gap-3">
          {showCheckout ? (
            <>
              <MetaRow label="Invoice Date" value={formatDate(item.invoiceDate)} />
              <MetaRow label="Due Date" value={formatDate(item.dueDate)} />
            </>
          ) : (
            <>
              <MetaRow label="Paid by Firm" value={formatDate(item.paidByFirmAt)} />
              <MetaRow label="Invoiced" value={formatDate(item.invoicedAt)} />
            </>
          )}
          {showCheckout && item.periodStart && (
            <MetaRow
              label="Period"
              value={`${formatDate(item.periodStart) ?? '…'} – ${formatDate(item.periodEnd) ?? '…'}`}
            />
          )}
          {showCheckout && item.periodEnd && !item.periodStart && (
            <MetaRow label="Period End" value={formatDate(item.periodEnd)} />
          )}
          {kind === 'gov' && <MetaRow label="Reimbursed" value={formatDate(item.reimbursedAt)} />}
          <MetaRow label="Created" value={formatDateTime(item.createdAt)} />
          <MetaRow label="Last Updated" value={formatDateTime(item.updatedAt)} />
        </div>

        {/* Notes */}
        {item.notes && (
          <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3 text-xs">
            <span className="font-medium text-[#6B7280]">Notes</span>
            <p className="mt-1.5 text-[#16181D] break-words">{item.notes}</p>
          </div>
        )}

        {/* Actions.
            While confirming, the checkout button is withdrawn so the customer
            cannot start a second payment against an order that is already
            being settled - the confirming banner is the only thing shown. */}
        {showCheckout && item.status === 'Pending' && paymentState !== 'confirming' && (
          <div className="flex flex-col gap-2.5">
            {showPdf && <DownloadPdfButton kind={kind} id={item.id} variant="primary" label="View Invoice PDF" />}
            <PaymentCheckout
              kind={kind}
              item={item}
              label={kind === 'retainer' ? 'Retainer invoice' : 'Service fee invoice'}
              onPaymentSubmitted={handlePaymentSubmitted}
            />
          </div>
        )}

        {showCheckout && item.status === 'Pending' && paymentState === 'confirming' && (
          <div className="flex flex-col gap-2.5">
            {showPdf && <DownloadPdfButton kind={kind} id={item.id} variant="primary" label="View Invoice PDF" />}
          </div>
        )}

        {showCheckout && item.status !== 'Pending' && (
          <div className="flex flex-col gap-2.5">
            {showPdf && <DownloadPdfButton kind={kind} id={item.id} variant="primary" label="View Invoice PDF" />}
          </div>
        )}

        {kind === 'gov' && (
          <p className="text-xs leading-relaxed text-[#9CA3AF]">
            Government fee disbursements are settled directly with the firm and cannot be paid self-service.
          </p>
        )}
      </div>
    )
  }

  return (
    <Drawer title={drawerConfig.title} icon={drawerConfig.icon} subtitle={drawerConfig.subtitle} onClose={onClose}>
      {content}
    </Drawer>
  )
}

export default InvoiceDetailDrawer