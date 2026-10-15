import { ListChecks, RotateCcw } from 'lucide-react'
import {
  GOV_FEE_STATUS_KEYS,
  INVOICE_STATUS_KEYS,
  NO_MONEY_AGGREGATION_NOTE,
  PAYMENT_STATUS_COUNT_COPY,
  govFeeStatusText,
  retainerStatusText,
  serviceFeeStatusText,
} from './billingDisplay'
import { useAdminGovFeeDisbursements, useAdminRetainerInvoices, useAdminServiceFeeInvoices } from '../../../hooks/admin/useAdminInvoices'
import { useAdminPaymentOrders } from '../../../hooks/admin/useAdminPayments'

/**
 * Per-status counts for the ACTIVE billing tab only.
 *
 * ------------------------------------------------------------------
 * WHERE EVERY NUMBER COMES FROM.
 * ------------------------------------------------------------------
 * Each count is a `totalCount` read from the SAME list endpoint the table below it
 * uses, requested with `pageSize: 1`. That is deliberate and it is the only honest
 * source available: the response carries a real filtered `totalCount` from the same
 * WHERE clause as the rows, so a count and the list beside it can never disagree,
 * and requesting one row instead of twenty costs nothing extra in payload.
 *
 * There is NO aggregate endpoint for any of these resources, so the alternative
 * would have been to page every record in the system and tally it in the browser —
 * an unbounded walk presented with a server's authority. Rejected.
 *
 * DashboardSummaryDto's `pendingRetainerInvoices` / `pendingServiceFeeInvoices` are
 * NOT used either, and they are not a shortcut: the dashboard query filters
 * `Status = 0` while the enum's Pending is `1`, so those two fields never count a
 * pending invoice. A card built on them would confidently read zero.
 *
 * `pageSize: 1` makes each count its own cache entry rather than one shared with the
 * `pageSize: 20` list, which is correct: they are different queries, and a count must
 * not be invalidated or satisfied by the page of rows currently on screen.
 *
 * ------------------------------------------------------------------
 * NO COUNT EVER READS AS ZERO WHEN IT IS UNKNOWN.
 * ------------------------------------------------------------------
 * `totalCount` falls back to 0 while loading and after a failure. Rendering that 0
 * would be a confident wrong number at exactly the two moments that matter most, so
 * loading shows a skeleton and failure shows an explicit dash marked Unavailable with
 * a retry. A genuine zero is a real zero and is shown as 0.
 *
 * ------------------------------------------------------------------
 * NO MONEY FIGURE APPEARS HERE.
 * ------------------------------------------------------------------
 * `currency` is unvalidated free text on every billing table and nothing in the
 * platform aggregates by currency, so there is no correct way to total amounts in
 * the browser. These cards therefore count RECORDS, never money: no outstanding, no
 * billed, no collected, and nothing that could be added across tabs.
 *
 * ONLY THE ACTIVE TAB'S CARDS ARE MOUNTED. Each tab's counts live in their own small
 * component, so switching tabs mounts that tab's hooks and unmounts the rest. The
 * alternative — calling all four list hooks unconditionally and disabling three — is
 * not available, because the list hooks bind no `enabled` flag and a hook call cannot
 * be made conditional. One component per tab is what keeps opening the page to the
 * active tab's requests and no more.
 */

function CountTile({ label, totalCount, isLoading, isError, onRetry, noun }) {
  const available = !isLoading && !isError

  return (
    <div className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]">
      <p className="text-xs font-semibold text-[#6B7280]">{label}</p>

      {isLoading ? (
        <div className="mt-2.5" aria-hidden="true">
          <div className="h-7 w-12 animate-pulse rounded-[6px] bg-[#F7F8FA]" />
        </div>
      ) : available ? (
        <p className="mt-2.5 text-2xl font-bold leading-none tracking-tight tabular-nums text-[#16181D]">
          {totalCount.toLocaleString('en-US')}
          <span className="sr-only"> {label} {noun}</span>
        </p>
      ) : (
        <div className="mt-2.5">
          <p className="text-2xl font-bold leading-none tracking-tight text-[#9CA3AF]">
            &mdash;
            <span className="sr-only"> {label} count unavailable</span>
          </p>
          <p className="mt-1.5 text-[11px] text-[#6B7280]">Unavailable</p>
        </div>
      )}

      {isError && onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="mt-2 inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2 py-1 text-[11px] font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
        >
          <RotateCcw size={11} strokeWidth={2} aria-hidden="true" />
          Retry
          <span className="sr-only"> loading the {label} count</span>
        </button>
      )}
    </div>
  )
}

/**
 * Wraps the tiles with the shared heading and scope note.
 *
 * There is deliberately NO group-level error banner and NO group-level retry. Every
 * count here is a separate `pageSize: 1` request with its own `isError` and its own
 * `refresh`, and each tile reports and retries its own failure — so a group banner
 * would either be a ninth place for the same failure to appear, or would have to
 * stand in for a tile that has not failed yet and thereby declare a healthy count
 * unavailable. The tiles own the failure; the group owns only the heading and the
 * note, which are statements about all of them rather than about any one.
 */
function CountGroup({ headingId, heading, subtitle, children, note }) {
  return (
    <section aria-labelledby={headingId} className="mb-5">
      <div className="mb-3 min-w-0">
        <h2 id={headingId} className="text-sm font-semibold tracking-tight text-[#16181D]">
          {heading}
        </h2>
        <p className="mt-0.5 text-xs text-[#6B7280]">{subtitle}</p>
      </div>

      <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-3">{children}</div>

      <p className="mt-3 flex items-start gap-2 text-xs text-[#6B7280]">
        <ListChecks size={14} strokeWidth={1.75} aria-hidden="true" className="mt-px shrink-0" />
        <span>{note}</span>
      </p>
    </section>
  )
}

const SCOPE_NOTE =
  'Each figure is the total the list endpoint reports for that status, at the same ' +
  'company and scope as the list below, read from the same query. Amounts are never ' +
  'added together: the currency column is unvalidated free text and nothing ' +
  'aggregates by currency, so a money total could silently mix units. '

function RetainerCounts({ clientCompanyId, serviceContractId }) {
  return (
    <>
      {INVOICE_STATUS_KEYS.map((status) => (
        <RetainerCountTile
          key={status}
          status={status}
          clientCompanyId={clientCompanyId}
          serviceContractId={serviceContractId}
        />
      ))}
    </>
  )
}

/**
 * One component per count, rather than one component looping over three hooks.
 * A hook call cannot be made conditional, so this file's only way to fetch one
 * status at a time is to let each count own its own hook.
 */
function RetainerCountTile({ status, clientCompanyId, serviceContractId }) {
  const { totalCount, isLoading, isError, refresh } = useAdminRetainerInvoices({
    clientCompanyId,
    serviceContractId,
    status,
    page: 1,
    pageSize: 1,
  })

  return (
    <CountTile
      label={`Retainer · ${retainerStatusText(status) ?? status}`}
      totalCount={totalCount}
      isLoading={isLoading}
      isError={isError}
      onRetry={refresh}
      noun="invoices"
    />
  )
}

function ServiceFeeCountTile({ status, clientCompanyId, renewalTaskId }) {
  const { totalCount, isLoading, isError, refresh } = useAdminServiceFeeInvoices({
    clientCompanyId,
    renewalTaskId,
    status,
    page: 1,
    pageSize: 1,
  })

  return (
    <CountTile
      label={`Service Fee · ${serviceFeeStatusText(status) ?? status}`}
      totalCount={totalCount}
      isLoading={isLoading}
      isError={isError}
      onRetry={refresh}
      noun="invoices"
    />
  )
}

function ServiceFeeCounts({ clientCompanyId, renewalTaskId }) {
  return (
    <>
      {INVOICE_STATUS_KEYS.map((status) => (
        <ServiceFeeCountTile
          key={status}
          status={status}
          clientCompanyId={clientCompanyId}
          renewalTaskId={renewalTaskId}
        />
      ))}
    </>
  )
}

function GovFeeCountTile({ status, clientCompanyId }) {
  const { totalCount, isLoading, isError, refresh } = useAdminGovFeeDisbursements({
    clientCompanyId,
    status,
    page: 1,
    pageSize: 1,
  })

  return (
    <CountTile
      label={govFeeStatusText(status) ?? status}
      totalCount={totalCount}
      isLoading={isLoading}
      isError={isError}
      onRetry={refresh}
      noun="disbursements"
    />
  )
}

function GovFeeCounts({ clientCompanyId }) {
  return (
    <>
      {GOV_FEE_STATUS_KEYS.map((status) => (
        <GovFeeCountTile key={status} status={status} clientCompanyId={clientCompanyId} />
      ))}
    </>
  )
}

/**
 * ONE count for payments, and not six. A per-status payment count would have to come
 * from the same status filter the repository cannot execute, so the split is not
 * obtainable at all — a single total is shown instead of five dashes.
 */
function PaymentCounts({ clientCompanyId }) {
  const { totalCount, isLoading, isError, refresh } = useAdminPaymentOrders({
    clientCompanyId,
    page: 1,
    pageSize: 1,
  })

  return (
    <div className="sm:col-span-3">
      <CountTile
        label="Total payment orders"
        totalCount={totalCount}
        isLoading={isLoading}
        isError={isError}
        onRetry={refresh}
        noun="payment orders"
      />
    </div>
  )
}

/**
 * Renders only the active tab's counts. The per-tab components above are mounted
 * conditionally at COMPONENT level, which is what keeps the unused tabs' queries
 * from ever being issued.
 */
function AdminBillingSummaryCards({
  activeTab,
  clientCompanyId,
  serviceContractId,
  renewalTaskId,
}) {
  const scopeSuffix = clientCompanyId ? 'for the selected company' : 'across all companies'

  if (activeTab === 'retainer') {
    return (
      <CountGroup
        headingId="admin-billing-counts-heading"
        heading="Retainer invoices by status"
        subtitle={`Pending, Paid and Void ${scopeSuffix}.`}
        note={SCOPE_NOTE}
      >
        <RetainerCounts
          clientCompanyId={clientCompanyId}
          serviceContractId={serviceContractId}
        />
      </CountGroup>
    )
  }

  if (activeTab === 'service-fee') {
    return (
      <CountGroup
        headingId="admin-billing-counts-heading"
        heading="Service Fee invoices by status"
        subtitle={`Pending, Paid and Void ${scopeSuffix}.`}
        note={SCOPE_NOTE}
      >
        <ServiceFeeCounts
          clientCompanyId={clientCompanyId}
          renewalTaskId={renewalTaskId}
        />
      </CountGroup>
    )
  }

  if (activeTab === 'gov-fee') {
    return (
      <CountGroup
        headingId="admin-billing-counts-heading"
        heading="Government fee disbursements by status"
        subtitle={`Paid by firm, invoiced to client and reimbursed ${scopeSuffix}.`}
        note={SCOPE_NOTE}
      >
        <GovFeeCounts clientCompanyId={clientCompanyId} />
      </CountGroup>
    )
  }

  return (
    <CountGroup
      headingId="admin-billing-counts-heading"
      heading="Payment orders"
      subtitle={`Total payment orders ${scopeSuffix}.`}
      note={PAYMENT_STATUS_COUNT_COPY + ' ' + NO_MONEY_AGGREGATION_NOTE}
    >
      <PaymentCounts clientCompanyId={clientCompanyId} />
    </CountGroup>
  )
}

export default AdminBillingSummaryCards
