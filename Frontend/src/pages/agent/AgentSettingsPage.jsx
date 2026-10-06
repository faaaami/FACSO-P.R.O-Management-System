import { Calendar, Settings } from 'lucide-react'
import { format } from 'date-fns'
import SectionCard from '../../components/client/SectionCard'
import LoadingState from '../../components/client/LoadingState'
import ErrorState from '../../components/client/ErrorState'
import AgentPageHeader from '../../components/agent/common/AgentPageHeader'
import AgentProfileForm from '../../components/agent/settings/AgentProfileForm'
import AgentChangePasswordForm from '../../components/agent/settings/AgentChangePasswordForm'
import { useAgentProfile } from '../../hooks/agent/useAgentProfile'

const PORTAL_BADGE = (
  <span className="inline-flex items-center gap-1.5 rounded-[6px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74]">
    <span className="h-1.5 w-1.5 rounded-full bg-[#0F9D74]" aria-hidden="true" />
    Agent Portal
  </span>
)

function AgentSettingsHeader() {
  const today = format(new Date(), 'EEEE, dd MMMM yyyy')

  return (
    <AgentPageHeader
      title="Settings"
      badge={PORTAL_BADGE}
      description="Manage your profile and account security."
      meta={
        <div className="flex shrink-0 items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs shadow-[0_1px_2px_rgba(28,31,38,0.04)]">
          <Calendar size={14} className="text-[#6B7280]" aria-hidden="true" />
          <span className="font-medium text-[#16181D]">{today}</span>
        </div>
      }
    />
  )
}

function AgentSettingsPage() {
  const { data, isLoading, isError, refetch } = useAgentProfile()

  return (
    <div className="space-y-6">
      <AgentSettingsHeader />

      {isLoading && <LoadingState label="Loading settings..." />}

      {!isLoading && isError && (
        <SectionCard title="Settings" icon={Settings} subtitle="Account and security settings">
          <ErrorState
            message="Unable to load your profile. Please try again."
            onRetry={() => refetch()}
          />
        </SectionCard>
      )}

      {!isLoading && !isError && data && (
        <>
          <AgentProfileForm profile={data} />
          <AgentChangePasswordForm />
        </>
      )}
    </div>
  )
}

export default AgentSettingsPage
