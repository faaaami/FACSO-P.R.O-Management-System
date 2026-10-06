import { CircleAlert, Info, RotateCw, X } from 'lucide-react'
import { INVOICE_STATUS_KEYS, PAYMENT_STATUS_FILTER_COPY, retainerStatusText, serviceFeeStatusText, govFeeStatusText } from './billingDisplay'

/**
 * The billing filter bar: ONE shared company selector, then only the filters the
 * active tab's endpoint actually binds.
 *
 * ------------------------------------------------------------------
 * THE COMPANY SELECTOR IS SHARED AND SITS ABOVE THE TABS.
 * ------------------------------------------------------------------
 * All four list endpoints bind `clientCompanyId`, and a reader asking "show me this
 * company's billing" means the same thing on all four tabs. So the company filter is
 * rendered ONCE, above the tablist, and it is never re-rendered per tab. Switching
 * tabs does not touch it, and the page resets every tab's page number when it
 * changes — because page 4 of one company's invoices has nothing to do with page 1
 * of another's.
 *
 * ------------------------------------------------------------------
 * EVERY CONTROL HERE BINDS A REAL PARAMETER. NOTHING ELSE IS OFFERED.
 * ------------------------------------------------------------------
 *   Retainer      GET /retainer-invoices        status, serviceContractId
 *   Service Fee   GET /service-fee-invoices     status, renewalTaskId
 *   Government    GET /gov-fee-disbursements    status            (no task filter,
 *                                                              no PDF)
 *   Payments      GET /payment-orders           NOTHING beyond the
 *                                               company, and that is a backend
 *                                               defect rather than a choice
 *
 * There is deliberately no free-text id box for a contract or a task. Those endpoints
 * bind a Guid, and a typed id is unverifiable input that produces a silent empty
 * result when it is wrong. Both controls are pickers fed by the company-scoped
 * endpoints instead.
 *
 * THE RELATIONSHIP PICKERS ARE COMPANY-GATED because the only endpoints that
 * enumerate contracts and tasks are company-scoped: GET /clients/{id}/contracts and
 * GET /admin/tasks?clientCompanyId=…. With no company chosen there is nothing to
 * enumerate, so the control states the requirement and stays disabled. It does NOT
 * render an empty dropdown, which would read as "this company has no contracts" —
 * a different and wrong claim.
 */
const selectClass =
  'w-full cursor-pointer rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-medium text-[#16181D] transition duration-150 hover:border-[#0F9D74]/25 focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] disabled:cursor-not-allowed disabled:bg-[#F7F8FA] disabled:text-[#9CA3AF]'

const labelClass = 'mb-1.5 block text-xs font-semibold text-[#6B7280]'

/** The status options for whichever enum the active tab uses. */
function statusOptionsFor(activeTab) {
  if (activeTab === 'gov-fee') {
    return ['PaidByFirm', 'InvoicedToClient', 'Reimbursed'].map((key) => ({
      value: key,
      label: govFeeStatusText(key),
    }))
  }

  const isRetainer = activeTab === 'retainer'
  return INVOICE_STATUS_KEYS.map((key) => ({
    value: key,
    label: isRetainer ? retainerStatusText(key) : serviceFeeStatusText(key),
  }))
}

/**
 * A disabled relationship picker that explains itself. Used for the contract and
 * task controls when no company is selected.
 */
function GatedControl({ id, label, hint }) {
  return (
    <div className="min-w-0">
      <label htmlFor={id} className={labelClass}>
        {label}
      </label>
      <select id={id} disabled value="" className={selectClass}>
        <option value="">Select a client company first</option>
      </select>
      <p className="mt-1.5 text-[11px] leading-relaxed text-[#9CA3AF]">{hint}</p>
    </div>
  )
}

function AdminBillingFilterBar({
  activeTab,
  clientCompanyId,
  status,
  serviceContractId,
  renewalTaskId,
  companyOptions,
  contractOptions,
  taskOptions,
  contractsEnabled,
  tasksEnabled,
  isContractsLoading,
  isContractsError,
  isTasksLoading,
  isTasksError,
  isFetching = false,
  onClientCompanyChange,
  onStatusChange,
  onServiceContractChange,
  onRenewalTaskChange,
  onClearFilters,
}) {
  const isRetainer = activeTab === 'retainer'
  const isServiceFee = activeTab === 'service-fee'
  const isGovFee = activeTab === 'gov-fee'
  const isPayments = activeTab === 'payments'

  const statusOptions = statusOptionsFor(activeTab)
  const hasRelationshipFilter = isRetainer || isServiceFee
  const hasActiveFilters = Boolean(clientCompanyId || status || serviceContractId || renewalTaskId)

  return (
    <div className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 p-3.5">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {/* The shared selector. It is outside the tab grid because it applies to
            every tab, and it is rendered once for the whole bar. */}
        <div className="min-w-0">
          <label htmlFor="admin-billing-filter-company" className={labelClass}>
            Client company
          </label>
          <select
            id="admin-billing-filter-company"
            value={clientCompanyId ?? ''}
            onChange={(event) => onClientCompanyChange(event.target.value || null)}
            className={selectClass}
          >
            <option value="">Any company</option>
            {companyOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
          <p className="mt-1.5 text-[11px] leading-relaxed text-[#9CA3AF]">
            Applies to all four tabs.
          </p>
        </div>

        {isPayments ? (
          <div className="min-w-0 sm:col-span-1 lg:col-span-2">
            {/*
              The absence of a status filter is EXPLAINED, not merely missing. A
              reader who knows payment orders have statuses would otherwise assume
              the page had simply forgotten the control.
            */}
            <p className="flex items-start gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-3 py-2.5 text-[11px] leading-relaxed text-[#6B7280]">
              <CircleAlert size={13} strokeWidth={1.75} className="mt-px shrink-0 text-[#D97706]" aria-hidden="true" />
              <span>
                <span className="font-semibold text-[#16181D]">No status filter.</span>{' '}
                {PAYMENT_STATUS_FILTER_COPY}
              </span>
            </p>
          </div>
        ) : (
          <div className="min-w-0">
            <label htmlFor="admin-billing-filter-status" className={labelClass}>
              Status
            </label>
            <select
              id="admin-billing-filter-status"
              value={status ?? ''}
              onChange={(event) => onStatusChange(event.target.value || null)}
              className={selectClass}
            >
              <option value="">All statuses</option>
              {statusOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
            <p className="mt-1.5 text-[11px] leading-relaxed text-[#9CA3AF]">
              {isGovFee
                ? 'How far the reimbursement has got: paid by the firm, invoiced to the client, or reimbursed.'
                : 'Pending, Paid or Void.'}
            </p>
          </div>
        )}

        {isRetainer &&
          (contractsEnabled ? (
            <div className="min-w-0">
              <label htmlFor="admin-billing-filter-contract" className={labelClass}>
                Service contract
              </label>
              <select
                id="admin-billing-filter-contract"
                value={serviceContractId ?? ''}
                onChange={(event) => onServiceContractChange(event.target.value || null)}
                disabled={isContractsLoading}
                className={selectClass}
              >
                <option value="">
                  {isContractsLoading
                    ? 'Loading contracts…'
                    : isContractsError
                      ? 'Could not load contracts'
                      : 'Any contract'}
                </option>
                {contractOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
              <p className="mt-1.5 text-[11px] leading-relaxed text-[#9CA3AF]">
                {isContractsError
                  ? 'The company’s contracts could not be loaded, so this filter is unavailable. The list below is unaffected.'
                  : 'This company’s contracts, from the company-scoped contract endpoint. The list is capped at the first 100.'}
              </p>
            </div>
          ) : (
            <GatedControl
              id="admin-billing-filter-contract"
              label="Service contract"
              hint="Contracts can only be listed for one company, because the Admin API has no global contract list. Choose a client company to narrow this list by contract."
            />
          ))}

        {isServiceFee &&
          (tasksEnabled ? (
            <div className="min-w-0">
              <label htmlFor="admin-billing-filter-task" className={labelClass}>
                Renewal task
              </label>
              <select
                id="admin-billing-filter-task"
                value={renewalTaskId ?? ''}
                onChange={(event) => onRenewalTaskChange(event.target.value || null)}
                disabled={isTasksLoading}
                className={selectClass}
              >
                <option value="">
                  {isTasksLoading
                    ? 'Loading renewal tasks…'
                    : isTasksError
                      ? 'Could not load renewal tasks'
                      : 'Any renewal task'}
                </option>
                {taskOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
              <p className="mt-1.5 text-[11px] leading-relaxed text-[#9CA3AF]">
                {isTasksError
                  ? 'This company’s renewal tasks could not be loaded, so this filter is unavailable. The list below is unaffected.'
                  : 'This company’s renewal tasks. The endpoint has no search parameter, so the list is capped at the first 100 and shown by id.'}
              </p>
            </div>
          ) : (
            <GatedControl
              id="admin-billing-filter-task"
              label="Renewal task"
              hint="Renewal tasks can only be listed for one company, because the Admin tasks endpoint is company-scoped and has no search. Choose a client company to narrow this list by task."
            />
          ))}
      </div>

      <div className="mt-3 flex flex-col gap-2.5 border-t border-[#E2E4E9] pt-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex min-w-0 flex-wrap items-center gap-2.5">
          {hasActiveFilters && (
            <button
              type="button"
              onClick={onClearFilters}
              className="inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-white focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
            >
              <X size={13} strokeWidth={2} aria-hidden="true" />
              Clear filters
            </button>
          )}

          {/* Kept separate from the initial-loading state so a background refetch
              never blanks the table it is updating. */}
          {isFetching && (
            <p
              role="status"
              className="flex items-center gap-1.5 text-xs font-medium text-[#6B7280]"
            >
              <RotateCw
                size={13}
                strokeWidth={1.75}
                className="animate-spin"
                aria-hidden="true"
              />
              Refreshing&hellip;
            </p>
          )}
        </div>

        <p className="flex min-w-0 items-start gap-1.5 text-[11px] leading-relaxed text-[#9CA3AF] sm:text-right">
          <Info size={12} strokeWidth={1.75} className="mt-0.5 shrink-0" aria-hidden="true" />
          <span>
            {hasRelationshipFilter
              ? 'Only the filters this endpoint really binds are offered. There is no text search, date filter or sorting control on any of these lists.'
              : isGovFee
                ? 'This endpoint binds no task filter and serves no PDF, so neither control is offered here.'
                : 'The only filter on this tab is the client company above.'}
          </span>
        </p>
      </div>
    </div>
  )
}

export default AdminBillingFilterBar
