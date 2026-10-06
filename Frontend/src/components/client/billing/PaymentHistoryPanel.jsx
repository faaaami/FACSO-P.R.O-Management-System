import { useState } from 'react'
import { History } from 'lucide-react'
import InvoiceTable from './InvoiceTable'
import { usePaymentHistory } from '../../../hooks/client/usePaymentHistory'
import { PAYMENT_STATUS, PAYMENT_INVOICE_TYPES, enumLabel } from '../enumLabels'
import { formatMoney, formatDateTime } from './format'

const PAGE_SIZE = 10

const selectClasses =
  'h-10 rounded-[10px] border border-[#E2E4E9] bg-white px-3.5 py-2 text-sm text-[#16181D] focus:border-[#0F9D74] focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] focus:outline-none transition duration-150'

function paymentTone(status) {
  if (status === 3) return 'success' // Paid
  if (status === 4) return 'danger' // Failed
  if (status === 6) return 'warning' // Refunded
  return 'neutral' // Created / Pending / Cancelled
}

function PaymentHistoryPanel() {
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)

  const params = { page, pageSize: PAGE_SIZE }
  if (status) params.status = status

  const { data, isLoading, isError, refetch } = usePaymentHistory(params)

  const changeStatus = (value) => {
    setStatus(value)
    setPage(1)
  }

  const config = {
    icon: History,
    title: 'Payment History',
    description: 'Payment orders created against your invoices',
    emptyIcon: History,
    emptyMessage: 'No payments found.',
    emptyDescription: 'No payment orders match the current filter settings.',
    itemNoun: 'payment',
    statusLabel: (item) => enumLabel(PAYMENT_STATUS, item.status) ?? 'N/A',
    statusTone: (item) => paymentTone(item.status),
    columns: [
      {
        key: 'type',
        header: 'Type',
        render: (item) => enumLabel(PAYMENT_INVOICE_TYPES, item.invoiceType) ?? 'Invoice',
      },
      {
        key: 'amount',
        header: 'Amount',
        align: 'right',
        render: (item) => (
          <span className="font-semibold text-[#16181D]">{formatMoney(item.amount, item.currency)}</span>
        ),
      },
      {
        key: 'created',
        header: 'Initiated',
        render: (item) => formatDateTime(item.createdAt) ?? <span className="text-[#9CA3AF]">–</span>,
      },
    ],
  }

  return (
    <div className="space-y-4">
      <section
        aria-label="Filter payment history"
        className="rounded-[12px] bg-white border border-[#E2E4E9] p-6 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
      >
        <label className="flex max-w-xs flex-col gap-1.5">
          <span className="text-xs font-medium text-[#6B7280]">Status</span>
          <select className={selectClasses} value={status} onChange={(e) => changeStatus(e.target.value)}>
            <option value="">All statuses</option>
            {Object.keys(PAYMENT_STATUS).map((key) => (
              <option key={key} value={key}>
                {PAYMENT_STATUS[key]}
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
        renderActions={null}
      />
    </div>
  )
}

export default PaymentHistoryPanel