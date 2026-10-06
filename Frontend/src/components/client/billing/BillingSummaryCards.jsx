import { FileText, Landmark, Receipt, Wallet } from 'lucide-react'
import { useRetainerInvoices } from '../../../hooks/client/useRetainerInvoices'
import { useServiceFeeInvoices } from '../../../hooks/client/useServiceFeeInvoices'
import { useGovFeeDisbursements } from '../../../hooks/client/useGovFeeDisbursements'
import { usePaymentHistory } from '../../../hooks/client/usePaymentHistory'

function SummaryMetric({ icon, label, hint, value, tone }) {
  const Icon = icon
  const iconTone = {
    success: 'bg-[rgba(15,157,116,0.08)] text-[#0F9D74] border border-[#0F9D74]/20',
    warning: 'bg-amber-50 text-[#D97706] border border-[#D97706]/20',
    danger: 'bg-red-50 text-[#DC2626] border border-[#DC2626]/20',
    neutral: 'bg-[#F7F8FA] text-[#6B7280] border border-[#E2E4E9]',
  }[tone]

  return (
    <div className="rounded-[12px] bg-white border border-[#E2E4E9] p-5 shadow-[0_1px_3px_rgba(28,31,38,0.06)]">
      <div className="flex items-center gap-3">
        <div className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-[8px] ${iconTone}`}>
          <Icon size={18} strokeWidth={1.75} aria-hidden="true" />
        </div>
        <div className="min-w-0">
          <p className="text-xs font-medium text-[#6B7280] uppercase tracking-wider">{label}</p>
          <p className="text-xs text-[#9CA3AF] truncate">{hint}</p>
        </div>
      </div>
      <div className="mt-4 flex items-baseline gap-2">
        <span className="text-3xl font-bold tracking-tight text-[#16181D]">{value}</span>
        <span className="text-xs text-[#6B7280]">items</span>
      </div>
    </div>
  )
}

function BillingSummaryCards() {
  const { data: retainerData } = useRetainerInvoices({ page: 1, pageSize: 1, status: 'Pending' })
  const { data: serviceFeeData } = useServiceFeeInvoices({ page: 1, pageSize: 1, status: 'Pending' })
  const { data: govFeeData } = useGovFeeDisbursements({ page: 1, pageSize: 1, status: 'InvoicedToClient' })
  const { data: paymentsData } = usePaymentHistory({ page: 1, pageSize: 1 })

  const pendingRetainer = retainerData?.totalCount ?? 0
  const pendingServiceFee = serviceFeeData?.totalCount ?? 0
  const dueGovFee = govFeeData?.totalCount ?? 0
  const totalPayments = paymentsData?.totalCount ?? 0

  return (
    <section
      aria-label="Billing Summary"
      className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4"
    >
      <SummaryMetric
        icon={Receipt}
        label="Pending Retainer"
        hint="Retainer invoices awaiting payment"
        value={pendingRetainer}
        tone={pendingRetainer > 0 ? 'warning' : 'neutral'}
      />
      <SummaryMetric
        icon={FileText}
        label="Pending Service Fees"
        hint="Service fee invoices awaiting payment"
        value={pendingServiceFee}
        tone={pendingServiceFee > 0 ? 'warning' : 'neutral'}
      />
      <SummaryMetric
        icon={Landmark}
        label="Government Fees Due"
        hint="Disbursements invoiced to you"
        value={dueGovFee}
        tone={dueGovFee > 0 ? 'warning' : 'neutral'}
      />
      <SummaryMetric
        icon={Wallet}
        label="Payments Made"
        hint="Total payment orders on record"
        value={totalPayments}
        tone={totalPayments > 0 ? 'success' : 'neutral'}
      />
    </section>
  )
}

export default BillingSummaryCards