import { useState } from 'react'
import { Receipt } from 'lucide-react'
import InvoiceTable, { DetailsButton } from './InvoiceTable'
import DownloadPdfButton from './DownloadPdfButton'
import { useRetainerInvoices } from '../../../hooks/client/useRetainerInvoices'
import { RETAINER_INVOICE_STATUS, enumLabel } from '../enumLabels'
import { formatMoney, formatDate } from './format'

const PAGE_SIZE = 10

const selectClasses =
  'h-10 rounded-[10px] border border-[#E2E4E9] bg-white px-3.5 py-2 text-sm text-[#16181D] focus:border-[#0F9D74] focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] focus:outline-none transition duration-150'

function statusTone(status) {
  if (status === 'Paid') return 'success'
  if (status === 'Void') return 'neutral'
  return 'warning'
}

function RetainerInvoicesPanel({ onSelect }) {
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)

  const params = { page, pageSize: PAGE_SIZE }
  if (status) params.status = status

  const { data, isLoading, isError, refetch } = useRetainerInvoices(params)

  const changeStatus = (value) => {
    setStatus(value)
    setPage(1)
  }

  const config = {
    icon: Receipt,
    title: 'Retainer Invoices',
    description: 'Monthly retainer billing on your service contract',
    emptyIcon: Receipt,
    emptyMessage: 'No retainer invoices found.',
    emptyDescription: 'No retainer invoices match the current filter settings.',
    itemNoun: 'invoice',
    statusLabel: (item) => enumLabel(RETAINER_INVOICE_STATUS, item.status) ?? 'Pending',
    statusTone: (item) => statusTone(item.status),
    columns: [
      {
        key: 'number',
        header: 'Invoice No',
        render: (item) => (
          <span className="font-mono text-[13px]">{item.invoiceNumber ?? item.id.slice(0, 8)}</span>
        ),
      },
      {
        key: 'due',
        header: 'Due Date',
        render: (item) => formatDate(item.dueDate) ?? <span className="text-[#9CA3AF]">–</span>,
      },
      {
        key: 'period',
        header: 'Period',
        render: (item) =>
          item.periodStart || item.periodEnd ? (
            <span className="text-[#6B7280]">
              {formatDate(item.periodStart) ?? '…'} – {formatDate(item.periodEnd) ?? '…'}
            </span>
          ) : (
            <span className="text-[#9CA3AF]">–</span>
          ),
      },
      {
        key: 'amount',
        header: 'Amount',
        align: 'right',
        render: (item) => (
          <span className="font-semibold text-[#16181D]">{formatMoney(item.amount, item.currency)}</span>
        ),
      },
    ],
  }

  const renderActions = (item) => (
    <>
      <DownloadPdfButton kind="retainer" id={item.id} label="PDF" />
      {item.status === 'Pending' && (
        <button
          type="button"
          onClick={() => onSelect('retainer', item.id)}
          className="inline-flex items-center gap-1.5 rounded-[10px] bg-[#0F9D74] px-3.5 py-1.5 text-xs font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] hover:bg-[#0B7D5D] transition duration-150 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] cursor-pointer"
        >
          Pay
        </button>
      )}
      <DetailsButton onSelect={() => onSelect('retainer', item.id)} />
    </>
  )

  return (
    <div className="space-y-4">
      <section
        aria-label="Filter retainer invoices"
        className="rounded-[12px] bg-white border border-[#E2E4E9] p-6 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
      >
        <label className="flex max-w-xs flex-col gap-1.5">
          <span className="text-xs font-medium text-[#6B7280]">Status</span>
          <select className={selectClasses} value={status} onChange={(e) => changeStatus(e.target.value)}>
            <option value="">All statuses</option>
            {Object.keys(RETAINER_INVOICE_STATUS).map((key) => (
              <option key={key} value={key}>
                {RETAINER_INVOICE_STATUS[key]}
              </option>
            ))}
          </select>
        </label>
      </section>

      <InvoiceTable
        config={config}
        data={data}
        isLoading={isLoading}
        isError={isError}
        onRetry={() => refetch()}
        page={page}
        pageSize={PAGE_SIZE}
        onPageChange={setPage}
        renderActions={renderActions}
      />
    </div>
  )
}

export default RetainerInvoicesPanel