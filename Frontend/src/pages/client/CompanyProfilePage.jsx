import { format } from 'date-fns'
import { Building2, Calendar, ShieldCheck } from 'lucide-react'
import LoadingState from '../../components/client/LoadingState'
import ErrorState from '../../components/client/ErrorState'
import EmptyState from '../../components/client/EmptyState'
import CompanyOverviewCard from '../../components/client/company/CompanyOverviewCard'
import CompanyInformationCard from '../../components/client/company/CompanyInformationCard'
import RegistrationCard from '../../components/client/company/RegistrationCard'
import AccountStatusCard from '../../components/client/company/AccountStatusCard'
import { useClientCompany } from '../../hooks/client/useClientCompany'

function CompanyProfilePage() {
  const { data: company, isLoading, isError, refetch } = useClientCompany()

  const today = format(new Date(), 'EEEE, dd MMMM yyyy')

  return (
    <div className="space-y-6">
      {/* Page header */}
      <header className="flex flex-col gap-4 pb-2 border-b border-[#E2E4E9] md:flex-row md:items-end md:justify-between">
        <div>
          <div className="flex items-center gap-2.5">
            <h1 className="text-2xl font-bold tracking-tight text-[#16181D]">Company Profile</h1>
            <span className="inline-flex items-center gap-1.5 rounded-[6px] bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74] border border-[#0F9D74]/20">
              <ShieldCheck size={12} strokeWidth={2} aria-hidden="true" />
              Client Portal
            </span>
          </div>
          <p className="mt-1.5 text-sm text-[#6B7280]">
            {company?.companyName ? (
              <>
                Registered corporate information and account status for{' '}
                <span className="font-semibold text-[#16181D]">{company.companyName}</span>.
              </>
            ) : (
              'Registered corporate information and account status for your company.'
            )}
          </p>
        </div>
        <div className="flex items-center gap-3 text-xs text-[#6B7280] shrink-0">
          <div className="flex items-center gap-1.5 rounded-[8px] bg-white border border-[#E2E4E9] px-3 py-1.5 shadow-[0_1px_2px_rgba(28,31,38,0.04)]">
            <Calendar size={14} className="text-[#6B7280]" aria-hidden="true" />
            <span className="font-medium text-[#16181D]">{today}</span>
          </div>
        </div>
      </header>

      {isLoading ? (
        <LoadingState label="Loading company profile…" />
      ) : isError ? (
        <ErrorState message="Could not load company information." onRetry={() => refetch()} />
      ) : !company ? (
        <EmptyState
          icon={Building2}
          message="Company profile is not available."
          description="Corporate entity records could not be retrieved."
        />
      ) : (
        <>
          <CompanyOverviewCard company={company} />
          <CompanyInformationCard company={company} />
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
            <RegistrationCard company={company} />
            <AccountStatusCard company={company} />
          </div>
        </>
      )}
    </div>
  )
}

export default CompanyProfilePage