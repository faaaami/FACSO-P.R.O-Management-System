import { useState } from 'react'
import { Calendar, Receipt } from 'lucide-react'
import { format } from 'date-fns'
import BillingTabs from '../../components/client/billing/BillingTabs'
import BillingSummaryCards from '../../components/client/billing/BillingSummaryCards'
import RetainerInvoicesPanel from '../../components/client/billing/RetainerInvoicesPanel'
import ServiceFeeInvoicesPanel from '../../components/client/billing/ServiceFeeInvoicesPanel'
import GovFeeDisbursementsPanel from '../../components/client/billing/GovFeeDisbursementsPanel'
import PaymentHistoryPanel from '../../components/client/billing/PaymentHistoryPanel'
import InvoiceDetailDrawer from '../../components/client/billing/InvoiceDetailDrawer'

function BillingPaymentsPage() {
  const [activeTab, setActiveTab] = useState('retainer')
  const [selected, setSelected] = useState(null)

  const openInvoice = (kind, id, item) => setSelected({ kind, id, item })
  const closeInvoice = () => setSelected(null)

  const today = format(new Date(), 'EEEE, dd MMMM yyyy')

  return (
    <div className="space-y-6">
      {/* Page header */}
      <header className="flex flex-col gap-4 pb-2 border-b border-[#E2E4E9] md:flex-row md:items-end md:justify-between">
        <div>
          <div className="flex items-center gap-2.5">
            <h1 className="text-2xl font-bold tracking-tight text-[#16181D]">Billing &amp; Payments</h1>
            <span className="inline-flex items-center gap-1.5 rounded-[6px] bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74] border border-[#0F9D74]/20">
              <Receipt size={12} strokeWidth={2} aria-hidden="true" />
              Client Portal
            </span>
          </div>
          <p className="mt-1.5 text-sm text-[#6B7280]">
            Review your invoices, government fees and payment history, and settle outstanding balances online.
          </p>
        </div>
        <div className="flex items-center gap-3 text-xs text-[#6B7280] shrink-0">
          <div className="flex items-center gap-1.5 rounded-[8px] bg-white border border-[#E2E4E9] px-3 py-1.5 shadow-[0_1px_2px_rgba(28,31,38,0.04)]">
            <Calendar size={14} className="text-[#6B7280]" aria-hidden="true" />
            <span className="font-medium text-[#16181D]">{today}</span>
          </div>
        </div>
      </header>

      {/* Summary cards */}
      <BillingSummaryCards />

      {/* Section tabs */}
      <BillingTabs active={activeTab} onChange={setActiveTab} />

      {activeTab === 'retainer' && <RetainerInvoicesPanel onSelect={openInvoice} />}
      {activeTab === 'serviceFee' && <ServiceFeeInvoicesPanel onSelect={openInvoice} />}
      {activeTab === 'govFee' && <GovFeeDisbursementsPanel onSelect={openInvoice} />}
      {activeTab === 'payments' && <PaymentHistoryPanel />}

      {/* Detail drawer */}
      {selected && (
        <InvoiceDetailDrawer
          kind={selected.kind}
          id={selected.id}
          item={selected.item}
          onClose={closeInvoice}
        />
      )}
    </div>
  )
}

export default BillingPaymentsPage