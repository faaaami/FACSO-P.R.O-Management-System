import { useState } from 'react'
import { format } from 'date-fns'
import { Eye, Receipt, RefreshCw } from 'lucide-react'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import EmptyState from '../../client/EmptyState'
import StatusPill from '../../client/StatusPill'
import { SERVICE_FEE_INVOICE_STATUS, enumLabel } from '../../client/enumLabels'
import { useAgentTaskServiceFeeInvoice } from '../../../hooks/agent/useAgentTaskServiceFeeInvoice'

function formatDate(value) {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  return format(date, 'dd MMM yyyy')
}

function invoiceTone(status) {
  if (status === 'Paid') return 'success'
  if (status === 'Pending') return 'warning'
  return 'neutral'
}

function DetailRow({ label, value }) {
  return (
    <div className="flex items-start justify-between gap-3">
      <span className="shrink-0 text-xs text-[#6B7280]">{label}</span>
      <span className="text-xs font-medium text-[#16181D] break-words text-right">{value ?? 'N/A'}</span>
    </div>
  )
}

function NoInvoice() {
  return (
    <EmptyState
      icon={Receipt}
      message="No service fee invoice"
      description="This task does not have a service fee invoice available."
    />
  )
}

function AgentServiceFeeInvoice({ taskId }) {
  const [requested, setRequested] = useState(false)
  const { data: invoice, isLoading, isError, error, refetch } = useAgentTaskServiceFeeInvoice(
    taskId,
    { enabled: requested }
  )

  const status = error?.response?.status
  const isMissing = !isError || status === 404 || status === 400 || status === 422

  let content
  if (!requested) {
    content = null
  } else if (isLoading) {
    content = <LoadingState label="Loading service fee invoice..." />
  } else if (isError && !isMissing) {
    content = (
      <ErrorState message="Could not load the service fee invoice." onRetry={() => refetch()} />
    )
  } else if (isError || !invoice) {
    content = <NoInvoice />
  } else {
    const amount =
      invoice.amount != null ? `${invoice.amount}${invoice.currency ? ` ${invoice.currency}` : ''}` : null

    content = (
      <div className="space-y-2.5">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="font-mono text-sm font-semibold text-[#16181D] break-all">
            {invoice.invoiceNumber ?? 'Service fee invoice'}
          </p>
          <StatusPill
            label={enumLabel(SERVICE_FEE_INVOICE_STATUS, invoice.status) ?? String(invoice.status)}
            tone={invoiceTone(invoice.status)}
          />
        </div>
        <DetailRow label="Amount" value={amount} />
        <DetailRow label="Invoice date" value={formatDate(invoice.invoiceDate)} />
        <DetailRow label="Due date" value={formatDate(invoice.dueDate)} />
        <DetailRow label="Paid at" value={formatDate(invoice.paidAt)} />
        {invoice.description && (
          <p className="pt-1 text-xs text-[#6B7280] break-words">{invoice.description}</p>
        )}
      </div>
    )
  }

  return (
    <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">
          Service fee invoice
        </span>
        {requested ? (
          <button
            type="button"
            onClick={() => refetch()}
            disabled={isLoading}
            className="inline-flex items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#6B7280] hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:opacity-50 disabled:cursor-not-allowed transition duration-150 cursor-pointer"
          >
            <RefreshCw size={13} strokeWidth={2} aria-hidden="true" />
            Refresh
          </button>
        ) : (
          <button
            type="button"
            onClick={() => setRequested(true)}
            className="inline-flex items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#16181D] hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer"
          >
            <Eye size={13} strokeWidth={2} aria-hidden="true" />
            View service fee invoice
          </button>
        )}
      </div>

      {content && <div className="mt-3">{content}</div>}
    </div>
  )
}

export default AgentServiceFeeInvoice
