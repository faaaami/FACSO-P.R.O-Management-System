import { Mail, MapPin, Phone } from 'lucide-react'
import SectionCard from '../SectionCard'
import InfoRow from './InfoRow'

function CompanyInformationCard({ company }) {
  return (
    <SectionCard
      title="Company Information"
      icon={Mail}
      subtitle="Contact and location details from the registered profile"
    >
      <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        <div className="rounded-[10px] border border-[#E2E4E9] bg-white p-4">
          <InfoRow label="Phone" value={company.phone} icon={Phone} />
        </div>
        <div className="rounded-[10px] border border-[#E2E4E9] bg-white p-4">
          <InfoRow label="Email" value={company.email} icon={Mail} />
        </div>
        <div className="rounded-[10px] border border-[#E2E4E9] bg-white p-4 lg:col-span-2">
          <InfoRow label="Office Address" value={company.address} icon={MapPin} />
        </div>
        <div className="rounded-[10px] border border-[#E2E4E9] bg-white p-4">
          <InfoRow label="Emirate" value={company.emirate} icon={MapPin} />
        </div>
      </div>
    </SectionCard>
  )
}

export default CompanyInformationCard