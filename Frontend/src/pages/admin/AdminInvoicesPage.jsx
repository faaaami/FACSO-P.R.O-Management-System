import { useState } from 'react'
import { Inbox, Plus, RefreshCw, ShieldCheck } from 'lucide-react'
import AdminPageHeader from '../../components/admin/AdminPageHeader'
import SectionCard from '../../components/client/SectionCard'
import LoadingState from '../../components/client/LoadingState'
import ErrorState from '../../components/client/ErrorState'
import EmptyState from '../../components/client/EmptyState'
import Pagination from '../../components/client/billing/Pagination'
import AdminBillingTabs from '../../components/admin/invoices/AdminBillingTabs'
import AdminBillingFilterBar from '../../components/admin/invoices/AdminBillingFilterBar'
import AdminBillingSummaryCards from '../../components/admin/invoices/AdminBillingSummaryCards'
import AdminRetainerInvoiceTable from '../../components/admin/invoices/AdminRetainerInvoiceTable'
import AdminServiceFeeInvoiceTable from '../../components/admin/invoices/AdminServiceFeeInvoiceTable'
import AdminGovFeeDisbursementTable from '../../components/admin/invoices/AdminGovFeeDisbursementTable'
import AdminPaymentOrderTable from '../../components/admin/invoices/AdminPaymentOrderTable'
import AdminInvoiceDetailDrawer from '../../components/admin/invoices/AdminInvoiceDetailDrawer'
import AdminGovFeeDisbursementDetailDrawer from '../../components/admin/invoices/AdminGovFeeDisbursementDetailDrawer'
import AdminPaymentOrderDetailDrawer from '../../components/admin/invoices/AdminPaymentOrderDetailDrawer'
import AdminRetainerInvoiceFormDialog from '../../components/admin/invoices/AdminRetainerInvoiceFormDialog'
import AdminServiceFeeInvoiceFormDialog from '../../components/admin/invoices/AdminServiceFeeInvoiceFormDialog'
import AdminGovFeeDisbursementFormDialog from '../../components/admin/invoices/AdminGovFeeDisbursementFormDialog'
import AdminPaymentOrderFormDialog from '../../components/admin/invoices/AdminPaymentOrderFormDialog'
import {
  BILLING_CREATE_LABELS,
  BILLING_LIST_SUBTITLES,
  BILLING_PAGE_SUBTITLE,
  BILLING_TABS,
  SERVICE_FEE_EMPTY_TITLE,
  SERVICE_FEE_UNREACHABLE_NOTE,
} from '../../components/admin/invoices/billingDisplay'
import { useAdminBillingFilterOptions } from '../../hooks/admin/useAdminBillingFilterOptions'
import { useAdminEntityMaps } from '../../hooks/admin/useAdminEntityMaps'
import {
  useAdminGovFeeDisbursements,
  useAdminRetainerInvoices,
  useAdminServiceFeeInvoices,
} from '../../hooks/admin/useAdminInvoices'
import { useAdminPaymentOrders } from '../../hooks/admin/useAdminPayments'
import { extractApiErrorMessage } from '../../utils/apiError'

/**
 * Admin Portal — Invoices. Four tabs of read and write over four resources.
 *
 * ONE ROUTE, FOUR TABS, NO SUB-ROUTES. Retainer, Service Fee, Government Fees and
 * Payments are tabs of /admin/invoices rather than four pages, because they answer
 * one reader's question — what is this client company owed, and what has been paid? —
 * across four endpoints, and because the company filter has to apply to all four at
 * once. There is no /admin/invoices/:kind route and no ?tab= parameter, since no
 * route in the Admin API accepts one.
 *
 * BACKING QUERIES
 *   GET /api/v1/retainer-invoices?clientCompanyId&serviceContractId&status&page&pageSize
 *   GET /api/v1/service-fee-invoices?clientCompanyId&renewalTaskId&status&page&pageSize
 *   GET /api/v1/gov-fee-disbursements?clientCompanyId&status&page&pageSize
 *   GET /api/v1/payment-orders?clientCompanyId&page&pageSize
 *   GET /api/v1/clients/{id}/contracts, GET /api/v1/admin/tasks?clientCompanyId=…
 *                                                 (filter bar AND each create dialog,
 *                                                  company-gated in both places)
 *   GET /api/v1/admin/clients, /api/v1/admin/staff
 *                                                 (via the shared entity map, for the
 *                                                  two GUID-only company columns)
 *   GET /api/v1/retainer-invoices/{id}, /service-fee-invoices/{id},
 *   /gov-fee-disbursements/{id}, /payment-orders/{id}
 *   GET /api/v1/audit-log?entityType=…&entityId=…            (drawer, Activity tab only)
 *   GET /api/v1/retainer-invoices/{id}/pdf, /service-fee-invoices/{id}/pdf
 *                                                 (drawer, on demand only)
 *
 * BACKING WRITES
 *   POST   /api/v1/retainer-invoices            (retainer form dialog)
 *   POST   /api/v1/service-fee-invoices        (service-fee form dialog)
 *   POST   /api/v1/gov-fee-disbursements       (gov-fee form dialog)
 *   PATCH  /api/v1/retainer-invoices/{id}/mark-paid | /void
 *   PATCH  /api/v1/service-fee-invoices/{id}/mark-paid | /void
 *   PATCH  /api/v1/gov-fee-disbursements/{id}/status
 *   POST   /api/v1/payment-orders              (payment form dialog)
 *   POST   /api/v1/payment-orders/{id}/cancel
 *
 * WHAT THIS PAGE CAN DO: filter, page, inspect one record in full, download an
 * invoice PDF, raise each of the four record types, mark an invoice paid, void an
 * invoice, advance a government fee's status, and cancel a payment order.
 *
 * EVERY ONE OF THOSE WRITES IS ONE-WAY, AND THE UI SAYS SO BEFORE EACH ONE. There is
 * no reversing endpoint anywhere in the module, so an undo is not a feature that was
 * left out — it is not possible. That is why the mark-paid, void, status-advance,
 * payment-create and payment-cancel paths all confirm first, why a void reason is
 * offered even though the server makes it optional, and why the labels name the
 * consequence ("Raise payment order", not "Create") rather than the CRUD operation.
 *
 * The two invoice types expose the same two transitions, so they share one drawer
 * (`kind` switches the DTO, not the actions), and their invalidation differs only in
 * which list and detail keys it touches. A mark-paid also flips the linked payment
 * order to Paid in the same transaction, which the confirmation states, because that
 * is a second record changing and a confirmation describing one edit would understate
 * the button.
 *
 * NO WRITE IS BUILT ON A LIST FILTER. A create form asks for its own company and
 * loads its own contract, task or invoice options instead of inheriting
 * `clientCompanyId`, because that value is frequently `null` — "all companies" — which
 * cannot preselect anything, and a form silently scoped to the list you happened to be
 * reading is a form that writes to the wrong place.
 *
 * ------------------------------------------------------------------
 * ONE LIST QUERY IS LIVE AT A TIME, AND THAT IS A STRUCTURAL FACT.
 * ------------------------------------------------------------------
 * The list hooks bind no `enabled` flag and a hook call cannot be made conditional,
 * so one component calling all four would fire four requests on open. Each tab is
 * therefore its own component: React mounts only the active one, so its query runs
 * and the other three are never created. The cost is that per-tab state cannot live
 * inside the tab components, which is why page, status and relationship filters are
 * held here and passed down — otherwise a tab round-trip would silently reset the
 * reader to page 1 with no filters applied.
 *
 * PAGE NUMBERS RESET, STATUS AND RELATIONSHIP FILTERS DO NOT. Filtering within a tab
 * always starts again at page 1, because page 4 of an unfiltered list has no
 * relationship to page 1 of a filtered one. The filters survive a tab round-trip, so
 * returning to a tab shows the reader the same slice they left.
 *
 * THE COMPANY CHANGE RESETS EVERY PAGE AND BOTH RELATIONSHIP FILTERS. A contract or a
 * task chosen for the previous company cannot belong to the new one, so keeping the
 * selection would filter by an id the API accepts and can never match — an empty list
 * that reads as an answer.
 *
 * THE COUNTS SIT BELOW THE TABLIST, inside the tabpanel, not above the tabs. Their
 * headings name the tab they belong to, and placing them under the tablist means the
 * reader never has to work out which of four sets they are looking at.
 */
const PAGE_SIZE = 20

const TAB_PANEL_ID = 'admin-billing-panel'

const ADMIN_PORTAL_BADGE = (
  <span className="inline-flex items-center gap-1.5 rounded-[6px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74]">
    <ShieldCheck size={13} strokeWidth={2} aria-hidden="true" />
    Admin Portal
  </span>
)

/**
 * The list body, in the order the states matter: a failure is reported BEFORE an
 * empty result, because "the request failed" and "there is nothing here" are
 * different claims and only one of them is true. A failure is never rendered as an
 * empty table, and never as a total of zero.
 */
function ListBody({
  isLoading,
  isError,
  error,
  onRetry,
  items,
  loadingLabel,
  loadErrorMessage,
  emptyMessage,
  emptyDescription,
  children,
}) {
  if (isLoading) {
    return <LoadingState label={loadingLabel} />
  }

  if (isError) {
    return <ErrorState message={extractApiErrorMessage(error, loadErrorMessage)} onRetry={onRetry} />
  }

  if (items.length === 0) {
    return <EmptyState icon={Inbox} message={emptyMessage} description={emptyDescription} />
  }

  return children
}

/**
 * The card every tab's list sits in: the count badge, the refresh control and the
 * pagination are identical across all four, so they are written once here rather
 * than four times in the tabs.
 *
 * THE BADGE AND THE PAGINATION ARE BOTH WITHHELD WHILE LOADING OR AFTER A FAILURE,
 * because `totalCount` falls back to 0 in both states. A badge reading 0 during a
 * failure would be a confident wrong number, and a pagination control built on the
 * same 0 would offer a reader the ability to page through nothing.
 */
function BillingListSection({
  title,
  subtitle,
  totalCount,
  isLoading,
  isError,
  isFetching,
  onRefresh,
  page,
  onPageChange,
  itemLabel,
  itemLabelPlural,
  onCreate,
  createLabel,
  children,
}) {
  const showCountBadge = !isLoading && !isError

  return (
    <SectionCard
      title={title}
      icon={Inbox}
      subtitle={subtitle}
      badge={
        showCountBadge ? (
          <span className="rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold tabular-nums text-[#6B7280]">
            {totalCount.toLocaleString('en-US')}
            <span className="sr-only"> {itemLabelPlural}</span>
          </span>
        ) : null
      }
      action={
        /*
          THE HEADER CARRIES TWO CONTROLS, IN THIS ORDER: create first, refresh second.

          Create is the primary action of a tab whose whole point is that records can
          be raised, so it is the solid button and comes first; refresh is a
          re-read of what is already on screen and stays secondary. They are wrapped
          in a flex row with a gap because SectionCard's own `action` slot is a single
          node, and two siblings returned as a fragment would sit flush against each
          other and against the card edge.

          Refresh is disabled while a fetch is in flight but CREATE IS NOT, because
          they are unrelated: opening a form does not race a list request, and
          disabling "raise an invoice" because a background refresh happens to be
          running would be a confusing coupling of two unrelated requests.
        */
        <div className="flex flex-wrap items-center justify-end gap-2">
          {onCreate && (
            <button
              type="button"
              onClick={onCreate}
              className="inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] bg-[#1C1F26] px-3 py-1.5 text-xs font-semibold text-white transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] focus:ring-offset-2"
            >
              <Plus size={13} strokeWidth={2.5} aria-hidden="true" />
              {createLabel}
              <span className="sr-only"> in this list</span>
            </button>
          )}

          <button
            type="button"
            onClick={onRefresh}
            disabled={isFetching}
            className="inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] disabled:cursor-not-allowed disabled:opacity-50"
          >
            <RefreshCw
              size={13}
              strokeWidth={2}
              className={isFetching ? 'animate-spin' : ''}
              aria-hidden="true"
            />
            Refresh
            <span className="sr-only"> the {itemLabelPlural} list</span>
          </button>
        </div>
      }
    >
      {children}

      {showCountBadge && totalCount > PAGE_SIZE && (
        <Pagination
          page={page}
          pageSize={PAGE_SIZE}
          totalCount={totalCount}
          itemLabel={itemLabel}
          itemLabelPlural={itemLabelPlural}
          onPageChange={onPageChange}
        />
      )}
    </SectionCard>
  )
}

function AdminRetainerTab({ clientCompanyId, status, serviceContractId, page, onPageChange, onOpenDetails, onCreate }) {
  const {
    items,
    totalCount,
    isLoading,
    isError,
    isFetching,
    error,
    refresh,
  } = useAdminRetainerInvoices({ clientCompanyId, serviceContractId, status, page, pageSize: PAGE_SIZE })

  // One page-level resolver, not one per row: a table renders many rows and cannot
  // call a hook inside a loop. The Retainer and Service Fee list DTOs are the only
  // two that carry a bare company Guid; the other two already carry a name.
  const { resolveClient } = useAdminEntityMaps()

  const hasFilters = Boolean(clientCompanyId || status || serviceContractId)

  return (
    <BillingListSection
      title="Retainer invoices"
      subtitle={BILLING_LIST_SUBTITLES.retainer}
      totalCount={totalCount}
      isLoading={isLoading}
      isError={isError}
      isFetching={isFetching}
      onRefresh={() => refresh()}
      page={page}
      onPageChange={onPageChange}
      itemLabel="invoice"
      itemLabelPlural="invoices"
      onCreate={onCreate}
      createLabel={BILLING_CREATE_LABELS.retainer}
    >
      <ListBody
        isLoading={isLoading}
        isError={isError}
        error={error}
        onRetry={() => refresh()}
        items={items}
        loadingLabel="Loading retainer invoices…"
        loadErrorMessage="Could not load retainer invoices."
        emptyMessage="No retainer invoices."
        emptyDescription={
          hasFilters
            ? 'No retainer invoice matches the current filters. Clear them to see every company.'
            : 'Retainer invoices appear here once one is raised against a service contract. This list is unfiltered, so an empty result means none exist yet.'
        }
      >
        <AdminRetainerInvoiceTable
          items={items}
          resolveCompany={resolveClient}
          onOpenDetails={onOpenDetails}
        />
      </ListBody>
    </BillingListSection>
  )
}

function AdminServiceFeeTab({ clientCompanyId, status, renewalTaskId, page, onPageChange, onOpenDetails, onCreate }) {
  const {
    items,
    totalCount,
    isLoading,
    isError,
    isFetching,
    error,
    refresh,
  } = useAdminServiceFeeInvoices({ clientCompanyId, renewalTaskId, status, page, pageSize: PAGE_SIZE })

  const { resolveClient } = useAdminEntityMaps()

  return (
    <BillingListSection
      title="Service Fee invoices"
      subtitle={BILLING_LIST_SUBTITLES['service-fee']}
      totalCount={totalCount}
      isLoading={isLoading}
      isError={isError}
      isFetching={isFetching}
      onRefresh={() => refresh()}
      page={page}
      onPageChange={onPageChange}
      itemLabel="invoice"
      itemLabelPlural="invoices"
      onCreate={onCreate}
      createLabel={BILLING_CREATE_LABELS['service-fee']}
    >
      <ListBody
        isLoading={isLoading}
        isError={isError}
        error={error}
        onRetry={() => refresh()}
        items={items}
        loadingLabel="Loading Service Fee invoices…"
        loadErrorMessage="Could not load Service Fee invoices."
        // The one empty state in this module that is NOT "you filtered too hard".
        emptyMessage={SERVICE_FEE_EMPTY_TITLE}
        emptyDescription={SERVICE_FEE_UNREACHABLE_NOTE}
      >
        <AdminServiceFeeInvoiceTable
          items={items}
          resolveCompany={resolveClient}
          onOpenDetails={onOpenDetails}
        />
      </ListBody>
    </BillingListSection>
  )
}

function AdminGovFeeTab({ clientCompanyId, status, page, onPageChange, onOpenDetails, onCreate }) {
  const {
    items,
    totalCount,
    isLoading,
    isError,
    isFetching,
    error,
    refresh,
  } = useAdminGovFeeDisbursements({ clientCompanyId, status, page, pageSize: PAGE_SIZE })

  const hasFilters = Boolean(clientCompanyId || status)

  return (
    <BillingListSection
      title="Government fee disbursements"
      subtitle={BILLING_LIST_SUBTITLES['gov-fee']}
      totalCount={totalCount}
      isLoading={isLoading}
      isError={isError}
      isFetching={isFetching}
      onRefresh={() => refresh()}
      page={page}
      onPageChange={onPageChange}
      itemLabel="disbursement"
      itemLabelPlural="disbursements"
      onCreate={onCreate}
      createLabel={BILLING_CREATE_LABELS['gov-fee']}
    >
      <ListBody
        isLoading={isLoading}
        isError={isError}
        error={error}
        onRetry={() => refresh()}
        items={items}
        loadingLabel="Loading government fee disbursements…"
        loadErrorMessage="Could not load government fee disbursements."
        emptyMessage="No government fee disbursements."
        emptyDescription={
          hasFilters
            ? 'No disbursement matches the current filters. Clear them to see every company.'
            : 'A disbursement appears here once the firm has paid a government fee. This list is unfiltered, so an empty result means none exist yet.'
        }
      >
        <AdminGovFeeDisbursementTable items={items} onOpenDetails={onOpenDetails} />
      </ListBody>
    </BillingListSection>
  )
}

function AdminPaymentsTab({ clientCompanyId, page, onPageChange, onOpenDetails, onCreate }) {
  const {
    items,
    totalCount,
    isLoading,
    isError,
    isFetching,
    error,
    refresh,
  } = useAdminPaymentOrders({ clientCompanyId, page, pageSize: PAGE_SIZE })

  return (
    <BillingListSection
      title="Payment orders"
      subtitle={BILLING_LIST_SUBTITLES.payments}
      totalCount={totalCount}
      isLoading={isLoading}
      isError={isError}
      isFetching={isFetching}
      onRefresh={() => refresh()}
      page={page}
      onPageChange={onPageChange}
      itemLabel="payment order"
      itemLabelPlural="payment orders"
      onCreate={onCreate}
      createLabel={BILLING_CREATE_LABELS.payments}
    >
      <ListBody
        isLoading={isLoading}
        isError={isError}
        error={error}
        onRetry={() => refresh()}
        items={items}
        loadingLabel="Loading payment orders…"
        loadErrorMessage="Could not load payment orders."
        emptyMessage="No payment orders."
        emptyDescription={
          clientCompanyId
            ? 'This company has no payment orders. Clear the company filter to see every company.'
            : 'A payment order appears here once one is raised against an invoice. This list is unfiltered, so an empty result means none exist yet.'
        }
      >
        <AdminPaymentOrderTable items={items} onOpenDetails={onOpenDetails} />
      </ListBody>
    </BillingListSection>
  )
}

function AdminInvoicesPage() {
  const [activeTab, setActiveTab] = useState('retainer')
  const [clientCompanyId, setClientCompanyId] = useState(null)
  const [selectedId, setSelectedId] = useState(null)

  /**
   * WHICH CREATE DIALOG IS OPEN, as a `BILLING_TABS` key, or null for none.
   *
   * A single key rather than four booleans, for one reason that matters: only one
   * create form may be open at a time. Four independent booleans would permit two
   * forms on screen together, each with its own focus trap fighting for the keyboard
   * and its own scrim over the other.
   *
   * It is null rather than a boolean because the four forms are NOT
   * interchangeable — a payment order dialog is gated on Pending invoices, a
   * retainer form on an active contract — and because the form itself owns its
   * content, so the page has no business knowing which company is selected inside it.
   *
   * Closed on tab switch, for the same reason the detail drawer is: a form belonging
   * to a tab that is no longer on screen must not linger over a different list.
   */
  const [createTarget, setCreateTarget] = useState(null)

  /**
   * A create form is opened for the ACTIVE tab, and the button that opens it is
   * rendered inside that tab, so this cannot be passed a key for a tab that is not
   * showing. Reading `activeTab` here rather than capturing it keeps the four `onCreate`
   * handlers identical instead of four near-identical closures, and makes it
   * impossible for a tab to open the wrong form after a fast switch.
   */
  function openCreateDialog() {
    setCreateTarget(activeTab)
  }

  // Per-tab state, held here rather than inside the tab components so that it
  // survives the unmount that switching tabs causes.
  const [retainer, setRetainer] = useState({ page: 1, status: null, serviceContractId: null })
  const [serviceFee, setServiceFee] = useState({ page: 1, status: null, renewalTaskId: null })
  const [govFee, setGovFee] = useState({ page: 1, status: null })
  const [payments, setPayments] = useState({ page: 1 })

  const activeTabMeta = BILLING_TABS.find((tab) => tab.key === activeTab) ?? BILLING_TABS[0]

  /**
   * The bar is fed the ACTIVE TAB's filters, not all twelve. A government-fee status
   * shown on the retainer bar would offer a value the retainer endpoint rejects, and a
   * contract id on the payments bar would offer a filter that endpoint does not bind.
   * The payments arm carries no relationship filters, which is why both are read as
   * `?? null` below rather than assumed present.
   */
  const filterState =
    activeTab === 'retainer'
      ? retainer
      : activeTab === 'service-fee'
        ? serviceFee
        : activeTab === 'gov-fee'
          ? govFee
          : { status: null, serviceContractId: null, renewalTaskId: null }

  const filterOptions = useAdminBillingFilterOptions(clientCompanyId)

  function onStatusChange(value) {
    if (activeTab === 'retainer') setRetainer((current) => ({ ...current, status: value, page: 1 }))
    else if (activeTab === 'service-fee') setServiceFee((current) => ({ ...current, status: value, page: 1 }))
    else if (activeTab === 'gov-fee') setGovFee((current) => ({ ...current, status: value, page: 1 }))
  }

  function onServiceContractChange(value) {
    setRetainer((current) => ({ ...current, serviceContractId: value, page: 1 }))
  }

  function onRenewalTaskChange(value) {
    setServiceFee((current) => ({ ...current, renewalTaskId: value, page: 1 }))
  }

  function onClientCompanyChange(value) {
    setClientCompanyId(value)
    setRetainer((current) => ({ ...current, page: 1, serviceContractId: null }))
    setServiceFee((current) => ({ ...current, page: 1, renewalTaskId: null }))
    setGovFee((current) => ({ ...current, page: 1 }))
    setPayments({ page: 1 })
  }

  function onClearFilters() {
    setClientCompanyId(null)
    setRetainer({ page: 1, status: null, serviceContractId: null })
    setServiceFee({ page: 1, status: null, renewalTaskId: null })
    setGovFee({ page: 1, status: null })
    setPayments({ page: 1 })
  }

  function onTabChange(tab) {
    setActiveTab(tab)
    // The drawer belongs to a tab. Closing it on a switch means a record from a tab
    // that is no longer on screen can never be left open over a different list.
    setSelectedId(null)
    setCreateTarget(null)
  }

  return (
    <div className="space-y-6">
      <AdminPageHeader
        title="Invoices"
        subtitle={BILLING_PAGE_SUBTITLE}
        action={ADMIN_PORTAL_BADGE}
      />

      <AdminBillingFilterBar
        activeTab={activeTab}
        clientCompanyId={clientCompanyId}
        status={filterState.status}
        serviceContractId={filterState.serviceContractId ?? null}
        renewalTaskId={filterState.renewalTaskId ?? null}
        companyOptions={filterOptions.companyOptions}
        contractOptions={filterOptions.contractOptions}
        taskOptions={filterOptions.taskOptions}
        contractsEnabled={filterOptions.contractsEnabled}
        tasksEnabled={filterOptions.tasksEnabled}
        isContractsLoading={filterOptions.isContractsLoading}
        isContractsError={filterOptions.isContractsError}
        isTasksLoading={filterOptions.isTasksLoading}
        isTasksError={filterOptions.isTasksError}
        onClientCompanyChange={onClientCompanyChange}
        onStatusChange={onStatusChange}
        onServiceContractChange={onServiceContractChange}
        onRenewalTaskChange={onRenewalTaskChange}
        onClearFilters={onClearFilters}
      />

      <AdminBillingTabs activeTab={activeTab} onTabChange={onTabChange} tabPanelId={TAB_PANEL_ID} />

      <div
        id={TAB_PANEL_ID}
        role="tabpanel"
        aria-labelledby={`admin-billing-tab-${activeTab}`}
        className="space-y-5"
      >
        <div className="min-w-0">
          <h2 className="text-sm font-semibold tracking-tight text-[#16181D]">
            {activeTabMeta.title}
          </h2>
          <p className="mt-0.5 text-xs text-[#6B7280]">{activeTabMeta.subtitle}</p>
        </div>

        <AdminBillingSummaryCards
          activeTab={activeTab}
          clientCompanyId={clientCompanyId}
          serviceContractId={retainer.serviceContractId}
          renewalTaskId={serviceFee.renewalTaskId}
        />

        {activeTab === 'retainer' ? (
          <AdminRetainerTab
            clientCompanyId={clientCompanyId}
            status={retainer.status}
            serviceContractId={retainer.serviceContractId}
            page={retainer.page}
            onPageChange={(page) => setRetainer((current) => ({ ...current, page }))}
            onOpenDetails={setSelectedId}
            onCreate={openCreateDialog}
          />
        ) : null}

        {activeTab === 'service-fee' ? (
          <AdminServiceFeeTab
            clientCompanyId={clientCompanyId}
            status={serviceFee.status}
            renewalTaskId={serviceFee.renewalTaskId}
            page={serviceFee.page}
            onPageChange={(page) => setServiceFee((current) => ({ ...current, page }))}
            onOpenDetails={setSelectedId}
            onCreate={openCreateDialog}
          />
        ) : null}

        {activeTab === 'gov-fee' ? (
          <AdminGovFeeTab
            clientCompanyId={clientCompanyId}
            status={govFee.status}
            page={govFee.page}
            onPageChange={(page) => setGovFee((current) => ({ ...current, page }))}
            onOpenDetails={setSelectedId}
            onCreate={openCreateDialog}
          />
        ) : null}

        {activeTab === 'payments' ? (
          <AdminPaymentsTab
            clientCompanyId={clientCompanyId}
            page={payments.page}
            onPageChange={(page) => setPayments({ page })}
            onOpenDetails={setSelectedId}
            onCreate={openCreateDialog}
          />
        ) : null}
      </div>

      {selectedId && activeTab === 'retainer' && (
        <AdminInvoiceDetailDrawer
          kind="retainer"
          invoiceId={selectedId}
          onClose={() => setSelectedId(null)}
        />
      )}

      {selectedId && activeTab === 'service-fee' && (
        <AdminInvoiceDetailDrawer
          kind="service-fee"
          invoiceId={selectedId}
          onClose={() => setSelectedId(null)}
        />
      )}

      {selectedId && activeTab === 'gov-fee' && (
        <AdminGovFeeDisbursementDetailDrawer
          disbursementId={selectedId}
          onClose={() => setSelectedId(null)}
        />
      )}

      {selectedId && activeTab === 'payments' && (
        <AdminPaymentOrderDetailDrawer
          paymentOrderId={selectedId}
          onClose={() => setSelectedId(null)}
        />
      )}

      {/*
        THE FOUR CREATE FORMS.

        Mounted ONE AT A TIME by the single `createTarget` key, and each is a sibling of
        the drawers rather than a child of the tab it belongs to, so a form is never
        unmounted by something happening inside the list below it.

        NONE IS GIVEN THE PAGE'S COMPANY FILTER, and that is intentional rather than an
        oversight. Each dialog asks for its own company and loads its own contract,
        task and invoice options, because a form that inherited `clientCompanyId` would
        open pre-scoped to whatever list filter happened to be set — including "All
        companies", which is not a company at all and cannot preselect anything. The
        list filter scopes what is being listed; it does not scope what may be written.

        `onCreated` is deliberately NOT passed, so no form closes itself on success.
        Each dialog's own success path handles the outcome, and the underlying list
        has already been invalidated by the mutation. Nothing here needs to know.

        The three invoice forms and the payment form are gated inside themselves — on
        an active contract, on an Updated task, and on Pending invoices respectively —
        so the page does not pre-check anything, and a form opened with nothing
        available explains itself rather than appearing as an empty shell.
      */}
      {createTarget === 'retainer' && (
        <AdminRetainerInvoiceFormDialog
          open
          onClose={() => setCreateTarget(null)}
        />
      )}

      {createTarget === 'service-fee' && (
        <AdminServiceFeeInvoiceFormDialog
          open
          onClose={() => setCreateTarget(null)}
        />
      )}

      {createTarget === 'gov-fee' && (
        <AdminGovFeeDisbursementFormDialog
          open
          onClose={() => setCreateTarget(null)}
        />
      )}

      {createTarget === 'payments' && (
        <AdminPaymentOrderFormDialog
          open
          onClose={() => setCreateTarget(null)}
        />
      )}
    </div>
  )
}

export default AdminInvoicesPage
