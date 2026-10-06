import { FileSignature } from 'lucide-react'
import SectionCard from '../SectionCard'
import StatusPill from '../StatusPill'
import { SERVICE_CONTRACT_STATUS, enumLabel } from '../enumLabels'

function formatMoney(amount, currency = 'AED') {
  try {
    return new Intl.NumberFormat('en-AE', { style: 'currency', currency }).format(amount)
  } catch {
    return `${amount} ${currency}`
  }
}

function contractStatusTone(status) {
  return status === 'Active' ? 'success' : 'neutral'
}

function ContractSummary({ contract }) {
  return (
    <SectionCard
      title="Contract Agreement"
      icon={FileSignature}
      subtitle="Corporate PRO service agreement & retainer terms"
      badge={
        <StatusPill
          label={enumLabel(SERVICE_CONTRACT_STATUS, contract.status) ?? 'Active'}
          tone={contractStatusTone(contract.status)}
        />
      }
    >
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0">
          <span className="text-xs font-medium uppercase tracking-wider text-[#6B7280]">
            Contract Number
          </span>
          <p className="mt-1 truncate text-2xl font-bold tracking-tight text-[#16181D]">
            {contract.contractNumber}
          </p>
        </div>
        <div className="shrink-0 rounded-[10px] bg-[#F7F8FA] border border-[#E2E4E9] px-4 py-3">
          <span className="text-xs font-medium text-[#6B7280]">Retainer Fee</span>
          <p className="mt-0.5 text-xl font-bold text-[#0F9D74]">
            {formatMoney(contract.retainerAmount, 'AED')}
          </p>
        </div>
      </div>
    </SectionCard>
  )
}

export default ContractSummary