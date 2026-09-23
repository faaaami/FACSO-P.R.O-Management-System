import { Calendar, FileSignature } from 'lucide-react'
import { format } from 'date-fns'
import ContractSummary from '../../components/client/contracts/ContractSummary'
import ContractSchedule from '../../components/client/contracts/ContractSchedule'
import ContractTerms from '../../components/client/contracts/ContractTerms'
import EmptyState from '../../components/client/EmptyState'
import ErrorState from '../../components/client/ErrorState'
import LoadingState from '../../components/client/LoadingState'
import { useActiveContract } from '../../hooks/client/useActiveContract'

function ContractsPage() {
  const { data: contract, isLoading, isError, refetch } = useActiveContract()
  const today = format(new Date(), 'EEEE, dd MMMM yyyy')

  let content
  if (isLoading) {
    content = <LoadingState label="Loading active contract…" />
  } else if (isError) {
    content = (
      <ErrorState
        message="Could not load the active contract."
        onRetry={() => refetch()}
      />
    )
  } else if (!contract) {
    content = (
      <EmptyState
        icon={FileSignature}
        message="No active contract found."
        description="There is currently no active PRO retainer contract associated with your account."
      />
    )
  } else {
    content = (
      <div className="space-y-6">
        <ContractSummary contract={contract} />

        <div className="grid grid-cols-1 gap-6 lg:grid-cols-12">
          <div className="lg:col-span-8">
            <ContractTerms contract={contract} />
          </div>
          <div className="lg:col-span-4">
            <ContractSchedule contract={contract} />
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      {/* Page header */}
      <header className="flex flex-col gap-4 pb-2 border-b border-[#E2E4E9] md:flex-row md:items-end md:justify-between">
        <div>
          <div className="flex items-center gap-2.5">
            <h1 className="text-2xl font-bold tracking-tight text-[#16181D]">Contracts</h1>
            <span className="inline-flex items-center gap-1.5 rounded-[6px] bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74] border border-[#0F9D74]/20">
              <FileSignature size={12} strokeWidth={2} aria-hidden="true" />
              Client Portal
            </span>
          </div>
          <p className="mt-1.5 text-sm text-[#6B7280]">
            Your corporate PRO service agreement, retainer terms and contract schedule.
          </p>
        </div>
        <div className="flex items-center gap-3 text-xs text-[#6B7280] shrink-0">
          <div className="flex items-center gap-1.5 rounded-[8px] bg-white border border-[#E2E4E9] px-3 py-1.5 shadow-[0_1px_2px_rgba(28,31,38,0.04)]">
            <Calendar size={14} className="text-[#6B7280]" aria-hidden="true" />
            <span className="font-medium text-[#16181D]">{today}</span>
          </div>
        </div>
      </header>

      {content}
    </div>
  )
}

export default ContractsPage