import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getDashboardSummary, getDashboardActivityFeed, getDashboardRevenue } from '../../api/admin/dashboard'
import { getAdminStaffWorkload } from '../../api/admin/staff'
import { getAdminRetainerInvoices, getAdminServiceFeeInvoices } from '../../api/admin/invoices'
import { getAdminExpiringDocuments } from '../../api/admin/documents'
import { getAdminServiceRequests } from '../../api/admin/serviceRequests'

/**
 * Phase 1 Admin Dashboard hooks — exactly seven dashboard-specific requests, each
 * independent and parallel:
 *
 *   1. GET /api/v1/dashboard/summary
 *   2. GET /api/v1/retainer-invoices?status=Pending&page=1&pageSize=1
 *   3. GET /api/v1/service-fee-invoices?status=Pending&page=1&pageSize=1
 *   4. GET /api/v1/admin/staff/workload
 *   5. GET /api/v1/admin/documents/expiring?days=30&page=1&pageSize=8&includeExpired=false
 *   6. GET /api/v1/service-requests?page=1&pageSize=5
 *   7. GET /api/v1/dashboard/activity-feed?page=1&pageSize=8
 *
 * Every hook is separate so one failing panel cannot blank the page, and a page
 * requests only the widgets it actually renders.
 *
 * REMOVED IN PHASE 1 (deliberately):
 *  - expiry-alerts: the two /dashboard/expiry-alerts endpoints are superseded by
 *                  the /admin/documents/expiring registry, which pages properly,
 *                  is not capped at 20, and covers entity- and employee-owned
 *                  documents in one query.
 *  - /dashboard/staff-workload: its SQL mislabels Approved as InProgress and
 *                  Updated as Blocked and inner-joins away zero-task agents, so
 *                  workload is sourced from /admin/staff/workload instead.
 *
 * REVENUE, NARROWED TO A COUNT (see useAdminDashboardPaidInvoiceCount). The
 * original Phase 1 rule was "no revenue hook at all", so that a later edit could
 * not be tempted into adding money. That rule still holds for the MONEY: all
 * three revenue amounts are still withheld, because they are cross-currency sums
 * (see that hook). What is now wired is the one honest field in the same
 * response — InvoiceCount, a plain count of paid invoices. The guard has
 * therefore moved from "no hook exists" to "the hook exposes a count and no
 * amounts", and the amounts must stay unreachable from the returned object.
 */

const STALE_TIME_MS = 60_000

/**
 * The six renewal-task statuses, in verified ordinal order, matching the keys of
 * GetStaffWorkloadResponseDto.TasksByStatus. The backend always populates all six.
 */
export const RENEWAL_TASK_STATUS_KEYS = [
  'Submitted',
  'FeePaid',
  'AwaitingApproval',
  'Blocked',
  'Approved',
  'Updated',
]

function toCount(value) {
  return Number.isFinite(value) ? value : 0
}

/** Shared projection so every hook reports the same state vocabulary. */
function queryState(query) {
  return {
    loading: query.isLoading,
    isLoading: query.isLoading,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}

/**
 * GET /api/v1/dashboard/summary
 *
 * Portfolio scale only. The response also carries
 * pendingRetainerInvoices / pendingServiceFeeInvoices, but those are computed
 * with `status = 0` while Pending = 1, so they always read 0 — they are ignored
 * here and the real counts come from the invoice list endpoints instead.
 */
export function useAdminDashboardSummary() {
  const query = useQuery({
    queryKey: ['admin', 'dashboard', 'summary'],
    queryFn: getDashboardSummary,
    staleTime: STALE_TIME_MS,
    retry: 1,
  })

  const data = query.data

  /*
   * THE THREE PORTFOLIO FIELD NAMES ARE THE BACKEND'S, NOT THE UI'S.
   *
   * DashboardSummaryDto is
   *   (int TotalClients, int TotalEntities, int TotalEmployees,
   *    int ActiveRenewalTasks, int PendingRetainerInvoices,
   *    int PendingServiceFeeInvoices)
   * and ASP.NET's default camelCase policy makes the wire shape totalClients /
   * totalEntities / totalEmployees / activeRenewalTasks /
   * pendingRetainerInvoices / pendingServiceFeeInvoices. Nothing in the API
   * wrapper aliases or renames them.
   *
   * This mapping previously read `clientCompanies`, `legalEntities` and
   * `employees` — names that exist on NEITHER the record nor the JSON. All three
   * resolved to `undefined` on every successful response, and `DashboardKpiCard`
   * treats `undefined` exactly like a failure, so three Portfolio KPIs rendered
   * "Unavailable" permanently while the backend was returning real numbers the
   * whole time. The card's guard is why this was a silent bug rather than a
   * visible one: it refused to invent a zero, which is the right behaviour and
   * must not be changed to accommodate a wrong field name.
   *
   * `?? null` is retained deliberately. It is not a leftover from the bug: `null`
   * is the signal DashboardKpiCard needs in order to keep showing "Unavailable"
   * with a working Retry when the request genuinely fails. Coercing either value
   * to 0 here would convert a real failure into a plausible-looking measurement,
   * so the fallback stays `null` and never 0.
   */
  return {
    data,
    clientCompanies: data?.totalClients ?? null,
    legalEntities: data?.totalEntities ?? null,
    employees: data?.totalEmployees ?? null,
    activeRenewalTasks: data?.activeRenewalTasks ?? null,
    ...queryState(query),
  }
}

/**
 * True pending-invoice counts, one query per invoice type, each with its own
 * key so the two KPIs can succeed or fail independently.
 *
 * `pageSize=1` is deliberate: only `totalCount` is read, and the filtered COUNT
 * is a real COUNT(*) rather than the paged row length. The status filter is sent
 * as the enum NAME ("Pending") because query-string binding resolves enums by
 * name.
 */
export function useAdminPendingInvoiceCounts() {
  const retainerQuery = useQuery({
    queryKey: ['admin', 'dashboard', 'pending-invoices', { kind: 'retainer' }],
    queryFn: () => getAdminRetainerInvoices({ status: 'Pending', page: 1, pageSize: 1 }),
    staleTime: STALE_TIME_MS,
    retry: 1,
  })

  const serviceFeeQuery = useQuery({
    queryKey: ['admin', 'dashboard', 'pending-invoices', { kind: 'service-fee' }],
    queryFn: () => getAdminServiceFeeInvoices({ status: 'Pending', page: 1, pageSize: 1 }),
    staleTime: STALE_TIME_MS,
    retry: 1,
  })

  return {
    retainer: {
      count: retainerQuery.data?.totalCount ?? null,
      ...queryState(retainerQuery),
    },
    serviceFee: {
      count: serviceFeeQuery.data?.totalCount ?? null,
      ...queryState(serviceFeeQuery),
    },
  }
}

/**
 * GET /api/v1/admin/staff/workload
 *
 * The single workload source for the dashboard. One query serves both the
 * portfolio-wide six-status breakdown and the per-agent table, because they are
 * two views of the same rows — not two endpoints.
 *
 * This is the CORRECT workload SQL: six correctly named statuses, Blocked = 4,
 * and a LEFT JOIN so agents with no tasks still appear. It is scoped to assigned
 * tasks on users with the Agent role, so an unassigned task is invisible here;
 * the panel labels say "assigned" for that reason.
 *
 * `blockedTaskCount` and `activeTaskCount` arrive per agent but are not used for
 * the six-status figures: activeTaskCount repeats the 1,2,3,5 definition, and
 * the six-way split is summed from tasksByStatus so the totals and the per-agent
 * rows can never disagree.
 */
export function useAdminTaskStatusBreakdown() {
  const query = useQuery({
    queryKey: ['admin', 'dashboard', 'task-status-breakdown'],
    queryFn: getAdminStaffWorkload,
    staleTime: STALE_TIME_MS,
    retry: 1,
  })

  const staff = useMemo(() => (Array.isArray(query.data?.items) ? query.data.items : []), [query.data])

  const byStatus = useMemo(() => {
    const totals = {}
    for (const key of RENEWAL_TASK_STATUS_KEYS) {
      totals[key] = staff.reduce((sum, member) => sum + toCount(member?.tasksByStatus?.[key]), 0)
    }
    return totals
  }, [staff])

  const totalAssigned = useMemo(
    () => RENEWAL_TASK_STATUS_KEYS.reduce((sum, key) => sum + byStatus[key], 0),
    [byStatus],
  )

  return {
    data: query.data,
    staff,
    byStatus,
    totalAssigned,
    blockedAssigned: byStatus.Blocked,
    statusKeys: RENEWAL_TASK_STATUS_KEYS,
    ...queryState(query),
  }
}

/**
 * GET /api/v1/admin/documents/expiring — Query: days (30), page (1),
 * pageSize (8), includeExpired (false)
 *
 * Forward-looking only: includeExpired=false, so already-expired documents are
 * out of scope and the figure is "expiring soon", not "overdue".
 *
 * The registry returns no owner names — only clientEntityId / employeeId GUIDs,
 * and no entity-name lookup exists in the Admin API — so callers must not invent
 * them.
 */
export function useAdminExpiringPreview(params = {}) {
  const days = params?.days ?? 30
  const pageSize = params?.pageSize ?? 8

  const query = useQuery({
    queryKey: ['admin', 'dashboard', 'expiring-preview', { days, pageSize }],
    queryFn: () => getAdminExpiringDocuments({ days, page: 1, pageSize, includeExpired: false }),
    staleTime: STALE_TIME_MS,
    retry: 1,
  })

  return {
    data: query.data,
    items: Array.isArray(query.data?.items) ? query.data.items : [],
    totalCount: query.data?.totalCount ?? null,
    days,
    ...queryState(query),
  }
}

/**
 * GET /api/v1/dashboard/revenue — Query: period ("month" | "week")
 *
 * ONLY THE COUNT IS EXPOSED. The three money fields are deliberately absent from
 * this hook's return value, so no caller can render them by accident.
 *
 * DashboardRevenueDto is (decimal TotalRevenue, decimal RetainerRevenue,
 * decimal ServiceFeeRevenue, int InvoiceCount). Each of the three amounts is a
 * cross-currency sum: the repository UNIONs the retainer and service-fee invoice
 * tables and takes SUM("Amount") while never selecting Currency, so each figure
 * silently adds numbers denominated in different currencies. Rendering one with
 * any currency symbol — including the `formatCurrency` / `formatMoney` default of
 * AED — would assert a single-currency total the data cannot support. This is
 * the same defect that blocks the client service report, where the invoice DTO
 * does not even carry a currency field.
 *
 * `InvoiceCount` is a genuine count of paid invoices over the period and is
 * honest on its own. It is `null` when absent so DashboardKpiCard shows
 * "Unavailable" with a working Retry; coercing it to 0 would turn a failed
 * request into a plausible-looking measurement.
 *
 * `period` is clamped here rather than forwarded blindly. The backend checks
 * only `period == "week"` and falls back to a month for everything else, so an
 * unrecognised value would be answered with a month of data while the query key
 * claimed something else — two labels reading one number. Clamping keeps the
 * cache key and the response in agreement.
 */
export function useAdminDashboardPaidInvoiceCount(params = {}) {
  const period = params?.period === 'week' ? 'week' : 'month'

  const query = useQuery({
    queryKey: ['admin', 'dashboard', 'revenue', { period }],
    queryFn: () => getDashboardRevenue({ period }),
    staleTime: STALE_TIME_MS,
    retry: 1,
  })

  return {
    period,
    invoiceCount: Number.isFinite(query.data?.invoiceCount) ? query.data.invoiceCount : null,
    ...queryState(query),
  }
}

/**
 * GET /api/v1/service-requests — Query: page (1), pageSize (5)
 *
 * `totalCount` here is a genuine all-time count of non-deleted requests, NOT a
 * count of open or pending ones — the endpoint has no status filter, so the
 * figure must be labelled "all-time" wherever it is shown.
 */
export function useAdminServiceRequestPreview(params = {}) {
  const pageSize = params?.pageSize ?? 5

  const query = useQuery({
    queryKey: ['admin', 'dashboard', 'service-requests-preview', { pageSize }],
    queryFn: () => getAdminServiceRequests({ page: 1, pageSize }),
    staleTime: STALE_TIME_MS,
    retry: 1,
  })

  return {
    data: query.data,
    items: Array.isArray(query.data?.items) ? query.data.items : [],
    totalCount: query.data?.totalCount ?? null,
    ...queryState(query),
  }
}

/** GET /api/v1/dashboard/activity-feed — Query: page (1), pageSize (8) */
export function useAdminActivityFeed(params = {}) {
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 8

  const query = useQuery({
    queryKey: ['admin', 'dashboard', 'activity-feed', { page, pageSize }],
    queryFn: () => getDashboardActivityFeed({ page, pageSize }),
    staleTime: STALE_TIME_MS,
    retry: 1,
  })

  return {
    data: query.data,
    items: Array.isArray(query.data?.items) ? query.data.items : [],
    totalCount: query.data?.totalCount ?? null,
    ...queryState(query),
  }
}
