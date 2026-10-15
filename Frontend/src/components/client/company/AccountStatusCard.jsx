import { ShieldAlert, UserCheck } from 'lucide-react'
import SectionCard from '../SectionCard'
import StatusPill from '../StatusPill'
import InfoRow from './InfoRow'
import { formatDateTime } from '../billing/format'

function AccountStatusCard({ company }) {
  const isActive = company.isActive

  return (
    <SectionCard
      title="Account Status"
      icon={UserCheck}
      subtitle="Account health and record activity"
    >
      <div className="rounded-[10px] border border-[#E2E4E9] bg-white p-4">
        <div className="flex items-center justify-between gap-4">
          <span className="text-xs font-medium text-[#6B7280] uppercase tracking-wider">Status</span>
          <div className="flex items-center gap-2 flex-wrap justify-end">
            {company.isDeleted && (
              <StatusPill label="Account Deleted" tone="danger" />
            )}
            <StatusPill label={isActive ? 'Active' : 'Inactive'} tone={isActive ? 'success' : 'neutral'} />
          </div>
        </div>
      </div>

      <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2">
        <div className="rounded-[10px] border border-[#E2E4E9] bg-white p-4">
          <InfoRow label="Created At" value={formatDateTime(company.createdAt)} />
        </div>
        <div className="rounded-[10px] border border-[#E2E4E9] bg-white p-4">
          <InfoRow label="Last Updated" value={formatDateTime(company.updatedAt)} />
        </div>
      </div>

      {company.isDeleted && (
        <p className="mt-3 flex items-start gap-2 rounded-[8px] bg-red-50 p-3 text-xs font-medium text-[#DC2626] border border-red-200">
          <ShieldAlert size={14} className="shrink-0 mt-0.5" aria-hidden="true" />
          This account has been marked as deleted. Contact the PRO team for assistance.
        </p>
      )}
    </SectionCard>
  )
}

export default AccountStatusCard