import { Calendar, Settings } from 'lucide-react'
import { format } from 'date-fns'
import SectionCard from '../../components/client/SectionCard'
import LoadingState from '../../components/client/LoadingState'
import ErrorState from '../../components/client/ErrorState'
import ProfileForm from '../../components/client/settings/ProfileForm'
import ChangePasswordForm from '../../components/client/settings/ChangePasswordForm'
import { useClientProfile } from '../../hooks/client/useClientProfile'

function SettingsPage() {
  const { data, isLoading, isError, refetch } = useClientProfile()

  const today = format(new Date(), 'EEEE, dd MMMM yyyy')

  return (
    <div className="space-y-6">
      {/* Page header */}
      <header className="flex flex-col gap-4 pb-2 border-b border-[#E2E4E9] md:flex-row md:items-end md:justify-between">
        <div>
          <div className="flex items-center gap-2.5">
            <h1 className="text-2xl font-bold tracking-tight text-[#16181D]">Settings</h1>
            <span className="inline-flex items-center gap-1.5 rounded-[6px] bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74] border border-[#0F9D74]/20">
              <Settings size={12} strokeWidth={2} aria-hidden="true" />
              Client Portal
            </span>
          </div>
          <p className="mt-1.5 text-sm text-[#6B7280]">
            Manage your profile and account security.
          </p>
        </div>
        <div className="flex items-center gap-3 text-xs text-[#6B7280] shrink-0">
          <div className="flex items-center gap-1.5 rounded-[8px] bg-white border border-[#E2E4E9] px-3 py-1.5 shadow-[0_1px_2px_rgba(28,31,38,0.04)]">
            <Calendar size={14} className="text-[#6B7280]" aria-hidden="true" />
            <span className="font-medium text-[#16181D]">{today}</span>
          </div>
        </div>
      </header>

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
          <ProfileForm profile={data} />
          <ChangePasswordForm />
        </>
      )}
    </div>
  )
}

export default SettingsPage