import { FileText } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import AdminClientSection, { AdminClientRecordCard, AdminClientRecordList } from './AdminClientSection'
import { useAdminClientContracts } from '../../../hooks/admin/useAdminContracts'
import { serviceContractStatusLabel } from '../enumLabels'
import {
  contractStatusTone,
  displayText,
  formatCurrency,
  formatDate,
  isContractCurrent,
} from './clientDisplay'

const PAGE_SIZE = 20

/**
 * Service contracts for the selected company, read-only.
 * Backing query GET /api/v1/clients/{clientCompanyId}/contracts?page&pageSize
 *
 * NOTE THE ROUTE. Contracts are not an /admin/clients sub-resource. The verified
 * route is the bare /api/v1/clients/{id}/contracts, which is Admin-authorised but
 * not Admin-prefixed.
 *
 * THE NO-TOTAL RULE, AND WHY IT MATTERS. This endpoint returns `{ items }` and
 * nothing else — no totalCount, no page, no pageSize. Every other endpoint in the
 * Admin API returns the full `{ items, page, pageSize, totalCount }` envelope, so
 * it would be easy to read totalCount here, get undefined, and let it render as
 * zero. That would tell the user "0 contracts" while contracts are on screen, or
 * produce a pager with no destination. So there is no count and no pager here.
 * The list is simply the first 20 the API returned, and if it filled the page the
 * note says more may exist, because at that point the API cannot tell us.
 *
 * THE ACTIVE CONTRACT IS DERIVED, NOT FETCHED. There is a dedicated
 * /contracts/active route, but it filters on Status == Active only and never
 * compares dates, so a contract whose end date has passed still comes back as
 * "active". Deriving from the list lets this component apply the end-date check
 * and label such a contract honestly, and it costs one request fewer. A contract
 * is only called current when it is Active AND its end date has not passed.
 */
function AdminClientContractsSection({ clientId }) {
  const { items, isLoading, isError, error, refresh } = useAdminClientContracts(clientId, {
    page: 1,
    pageSize: PAGE_SIZE,
  })

  const filledThePage = items.length === PAGE_SIZE

  // A current contract, per the date-aware rule. Derived rather than fetched.
  const currentContract = items.find((contract) => isContractCurrent(contract)) ?? null

  // An Active contract that has already lapsed is worth calling out, because the
  // backend would report it as active and an Admin comparing the two views would
  // otherwise see a contradiction with no explanation.
  const lapsedButActive = items.find(
    (contract) => contract?.status === 'Active' && !isContractCurrent(contract),
  )

  function scopeNote() {
    if (items.length === 0) return null
    if (filledThePage) {
      return `Showing the first ${PAGE_SIZE} contracts. The API returns no total count, so more may exist beyond this page.`
    }
    return `Showing all ${items.length} contract${items.length === 1 ? '' : 's'}. The API returns no total count for this list.`
  }

  const note = scopeNote()

  return (
    <AdminClientSection
      title="Service contracts"
      description="Retainer and service agreements recorded against this company."
      loading={isLoading}
      loadingLabel="Loading contracts…"
      error={isError ? error : null}
      onRetry={() => refresh()}
      errorMessage="Could not load the contracts for this company."
      isEmpty={!isLoading && !isError && items.length === 0}
      emptyMessage="No contracts on file."
      emptyDescription="A service contract recorded against this company will appear here."
      emptyIcon={FileText}
    >
      <div className="flex flex-col gap-4">
        {currentContract && (
          <div className="rounded-[10px] border border-[#0F9D74]/25 bg-[rgba(15,157,116,0.06)] p-3.5">
            <p className="text-xs font-semibold uppercase tracking-wider text-[#0B7A5A]">
              Current contract
            </p>
            <p className="mt-1.5 break-words text-sm font-semibold text-[#16181D]">
              {displayText(currentContract?.contractNumber)}
            </p>
            <p className="mt-0.5 text-xs text-[#6B7280]">
              {formatDate(currentContract?.startDate)} →{' '}
              {currentContract?.endDate ? formatDate(currentContract.endDate) : 'No end date'} ·{' '}
              {formatCurrency(currentContract?.retainerAmount)}
            </p>
          </div>
        )}

        {!currentContract && lapsedButActive && (
          <div className="rounded-[10px] border border-amber-200 bg-amber-50/60 p-3.5">
            <p className="text-xs font-semibold uppercase tracking-wider text-[#92400E]">
              No current contract
            </p>
            <p className="mt-1.5 text-xs text-[#92400E]">
              Contract {displayText(lapsedButActive?.contractNumber)} is still marked
              Active but its end date has passed. The backend does not compare dates
              when deciding what is active.
            </p>
          </div>
        )}

        {note && (
          <p className="text-xs text-[#6B7280]">{note}</p>
        )}

        <AdminClientRecordList items={items}>
          {(contract) => {
            const status = serviceContractStatusLabel(contract?.status) ?? 'Unknown'
            const isCurrent = isContractCurrent(contract)

            return (
              <AdminClientRecordCard
                key={contract?.id}
                title={displayText(contract?.contractNumber)}
                subtitle={
                  currentContract?.id === contract?.id ? 'Shown as the current contract above' : undefined
                }
                trailing={
                  <StatusPill
                    label={isCurrent ? 'Current' : status}
                    tone={isCurrent ? 'success' : contractStatusTone(contract?.status)}
                  />
                }
                meta={[
                  {
                    label: 'Period',
                    value: `${formatDate(contract?.startDate)} → ${
                      contract?.endDate ? formatDate(contract.endDate) : 'No end date'
                    }`,
                  },
                  { label: 'Retainer', value: formatCurrency(contract?.retainerAmount) },
                  { label: 'Terms', value: displayText(contract?.terms) },
                ]}
              />
            )
          }}
        </AdminClientRecordList>
      </div>
    </AdminClientSection>
  )
}

export default AdminClientContractsSection
