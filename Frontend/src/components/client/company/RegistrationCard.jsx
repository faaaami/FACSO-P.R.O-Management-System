import { ShieldCheck } from 'lucide-react'
import SectionCard from '../SectionCard'
import InfoRow from './InfoRow'

function RegistrationCard({ company }) {
  return (
    <SectionCard
      title="Registration & License"
      icon={ShieldCheck}
      subtitle="Trade licensing details on record"
    >
      <div className="rounded-[10px] border border-[#E2E4E9] bg-white p-4">
        <InfoRow label="Trade License No." value={company.tradeLicenseNumber} mono />
      </div>
    </SectionCard>
  )
}

export default RegistrationCard