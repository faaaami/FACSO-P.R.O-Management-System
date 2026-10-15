import { FileText, History, Landmark, Receipt } from 'lucide-react'

function BillingTabs({ active, onChange }) {
  const tabs = [
    { id: 'retainer', label: 'Retainer Invoices', icon: Receipt },
    { id: 'serviceFee', label: 'Service Fee Invoices', icon: FileText },
    { id: 'govFee', label: 'Government Fees', icon: Landmark },
    { id: 'payments', label: 'Payment History', icon: History },
  ]

  return (
    <div
      role="tablist"
      aria-label="Billing sections"
      className="grid grid-cols-2 gap-2 rounded-[12px] bg-white border border-[#E2E4E9] p-1.5 shadow-[0_1px_3px_rgba(28,31,38,0.06)] lg:grid-cols-4"
    >
      {tabs.map((tab) => {
        const TabIcon = tab.icon
        const isActive = tab.id === active
        return (
          <button
            key={tab.id}
            type="button"
            role="tab"
            aria-selected={isActive}
            onClick={() => onChange(tab.id)}
            className={`flex items-center justify-center gap-2 rounded-[10px] px-3 py-2.5 text-sm font-semibold transition duration-150 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] cursor-pointer ${
              isActive
                ? 'bg-[rgba(15,157,116,0.08)] text-[#0F9D74] border border-[#0F9D74]/20'
                : 'text-[#6B7280] hover:bg-gray-50 hover:text-[#16181D]'
            }`}
          >
            <TabIcon size={15} strokeWidth={1.75} aria-hidden="true" />
            <span className="truncate">{tab.label}</span>
          </button>
        )
      })}
    </div>
  )
}

export default BillingTabs