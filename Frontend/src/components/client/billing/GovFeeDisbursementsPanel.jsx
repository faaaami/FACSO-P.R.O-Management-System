import { useState } from 'react'
import { Landmark } from 'lucide-react'
import InvoiceTable, { DetailsButton } from './InvoiceTable'
import { useGovFeeDisbursements } from '../../../hooks/client/useGovFeeDisbursements'
import { GOV_FEE_DISBURSEMENT_STATUS, enumLabel } from '../enumLabels'
import { formatMoney, formatDate } from './format'

const PAGE_SIZE = 10

const selectClasses =
  'h-10 rounded-[10px] border border-[#E2E4E9] bg-white px-3.5 py-2 text-sm text-[#16181D] focus:border-[#0F9D74] focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] focus:outline-none transition duration-150'

function statusTone(status) {
  if (status === 'Reimbursed') return 'success'
  if (status === 'InvoicedToClient') return 'warning'
  return 'neutral'
}

function GovFeeDisbursementsPanel({ onSelect }) {
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)

  const params = { page, pageSize: PAGE_SIZE }
  if (status) params.status = status

  const { data, isLoading, isError, refetch } = useGovFeeDisbursements(params)

  const changeStatus = (value) => {
    setStatus(value)
    setPage(1)
  }

  const config = {
    icon: Landmark,
    title: 'Government Fee Disbursements',
    description: 'Government fees paid by the firm and passed on to you',
    emptyIcon: Landmark,
    emptyMessage: 'No government fee disbursements found.',
    emptyDescription: 'No government fee disbursements match the current filter settings.',
    itemNoun: 'disbursement',
    statusLabel: (item) => enumLabel(GOV_FEE_DISBURSEMENT_STATUS, item.status) ?? 'N/A',
    statusTone: (item) => statusTone(item.status),
    columns: [
      {
        key: 'fee',
        header: 'Fee',
        render: (item) => (
          <span className="block max-w-[16rem] truncate">{item.feeDescription ?? item.id.slice(0, 8)}</span>
        ),
      },
      {
        key: 'reference',
        header: 'Gov Reference',
        render: (item) =>
          item.governmentReference ? (
            <span className="font-mono text-[13px]">{item.governmentReference}</span>
          ) : (
            <span className="text-[#9CA3AF]">–</span>
          ),
      },
      {
        key: 'paidByFirm',
        header: 'Paid by Firm',
        render: (item) => formatDate(item.paidByFirmAt) ?? <span className="text-[#9CA3AF]">–</span>,
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
    <DetailsButton onSelect={() => onSelect('gov', item.id, item)} />
  )

  return (
    <div className="space-y-4">
      <section
        aria-label="Filter government fee disbursements"
        className="rounded-[12px] bg-white border border-[#E2E4E9] p-6 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
      >
        <label className="flex max-w-xs flex-col gap-1.5">
          <span className="text-xs font-medium text-[#6B7280]">Status</span>
          <select className={selectClasses} value={status} onChange={(e) => changeStatus(e.target.value)}>
            <option value="">All statuses</option>
            {Object.keys(GOV_FEE_DISBURSEMENT_STATUS).map((key) => (
              <option key={key} value={key}>
                {GOV_FEE_DISBURSEMENT_STATUS[key]}
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

export default GovFeeDisbursementsPanel