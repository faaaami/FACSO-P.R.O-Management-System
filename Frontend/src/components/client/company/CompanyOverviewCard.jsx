import { Building2, Mail, MapPin, Phone, ShieldCheck } from 'lucide-react'
import SectionCard from '../SectionCard'

function CompanyOverviewCard({ company }) {
  const statCards = [
    {
      label: 'Trade License',
      icon: null,
      value: company.tradeLicenseNumber,
      mono: true,
    },
    {
      label: 'Emirate',
      icon: MapPin,
      value: company.emirate,
      mono: false,
    },
    {
      label: 'Phone',
      icon: Phone,
      value: company.phone,
      mono: false,
    },
    {
      label: 'Email',
      icon: Mail,
      value: company.email,
      mono: false,
    },
    {
      label: 'Office Address',
      icon: null,
      value: company.address,
      wide: true,
      mono: false,
    },
  ]

  return (
    <SectionCard
      title="Company Overview"
      icon={Building2}
      subtitle="Registered corporate identity and account summary"
      badge={
        <span className="inline-flex items-center gap-1 rounded-[6px] bg-[rgba(15,157,116,0.08)] px-2 py-0.5 text-xs font-semibold text-[#0F9D74] border border-[#0F9D74]/20">
          <ShieldCheck size={12} aria-hidden="true" />
          Verified Corporate Entity
        </span>
      }
    >
      <div className="flex items-center gap-3.5">
        <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-[10px] bg-[#1C1F26] text-[#0F9D74] shadow-[0_1px_3px_rgba(28,31,38,0.06)]">
          <Building2 size={22} strokeWidth={1.75} aria-hidden="true" />
        </div>
        <div className="min-w-0">
          <h3 className="text-lg font-bold tracking-tight text-[#16181D] break-words">{company.companyName}</h3>
          <p className="mt-0.5 text-xs text-[#6B7280]">Registered Client Organization &amp; Compliance Account</p>
        </div>
      </div>

      <div className="mt-5 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {statCards.map((item) => {
          const Icon = item.icon
          return (
            <div
              key={item.label}
              className={`rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] p-3 ${
                item.wide ? 'sm:col-span-2 lg:col-span-3' : ''
              }`}
            >
              <span className="block text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">
                {item.label}
              </span>
              <div className="mt-1 flex items-center gap-1.5">
                {Icon && <Icon size={13} className="shrink-0 text-[#6B7280]" aria-hidden="true" />}
                <p
                  className={`text-sm font-semibold text-[#16181D] break-words ${item.mono ? 'font-mono' : ''}`}
                  title={item.value ?? undefined}
                >
                  {item.value ?? 'N/A'}
                </p>
              </div>
            </div>
          )
        })}
      </div>
    </SectionCard>
  )
}

export default CompanyOverviewCard