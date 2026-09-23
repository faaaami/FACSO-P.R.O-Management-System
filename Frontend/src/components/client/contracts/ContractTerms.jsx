import { ScrollText } from 'lucide-react'
import SectionCard from '../SectionCard'

function ContractTerms({ contract }) {
  return (
    <SectionCard
      title="Scope & Terms"
      icon={ScrollText}
      subtitle="Service coverage, scope and agreement terms"
    >
      {contract.terms ? (
        <p className="text-sm leading-relaxed text-[#16181D] whitespace-pre-line">
          {contract.terms}
        </p>
      ) : (
        <p className="text-sm text-[#6B7280]">
          No terms have been recorded for this contract.
        </p>
      )}
    </SectionCard>
  )
}

export default ContractTerms