import apiClient from '../axios'

/**
 * Admin dashboard endpoints. `[Authorize(Roles = "Admin")]` under /api/v1/dashboard.
 *
 * WHAT PHASE 1 USES: summary, activity-feed. (revenue is wrapped here for a later
 * approved phase but the dashboard deliberately requests no money figures — see
 * useAdminDashboard.)
 *
 * WHAT PHASE 1 DOES NOT USE, and why:
 *
 *  - summary.PendingRetainerInvoices / PendingServiceFeeInvoices are computed with
 *    `status = 0`, but RetainerInvoiceStatus/ServiceFeeInvoiceStatus start at
 *    Pending = 1, so both counters ALWAYS read 0. They are ignored; the real
 *    counts come from the filtered invoice list endpoints (see
 *    useAdminPendingInvoiceCounts).
 *
 *  - summary.activeRenewalTasks is USED, but it is NOT a complete count of active
 *    work and must never be read as one. The backend query filters
 *    `status IN (1,2,3,5)`, which counts exactly:
 *        Submitted (1), FeePaid (2), AwaitingApproval (3), Approved (5)
 *    and therefore EXCLUDES:
 *        Blocked (4) and Updated (6)
 *    Blocked is excluded even though a blocked task is emphatically work still
 *    outstanding — but that is now a RESTORABLE pause rather than a permanent
 *    one: PATCH /admin/tasks/{id}/unblock returns it to `Submitted`, which IS in
 *    the counted set. The exclusion is therefore a real blind spot for as long as
 *    this query is not widened, and it would close on its own if someone added
 *    `Blocked` back — which would be wrong, because doing so would double-count a
 *    blocked task and a submitted one as the same open work. The number remains
 *    correct for what it claims and AdminDashboardPage labels it with precisely
 *    that definition, so no value is changed here; this note exists to stop a
 *    future edit from treating the figure as the portfolio's total open workload.
 *
 *  - The three remaining summary fields are real, correctly named counts
 *    (totalClients / totalEntities / totalEmployees). They are surfaced in
 *    useAdminDashboardSummary.
 *
 *  - /staff-workload is deliberately NOT wrapped. Its SQL inner-joins tasks (so
 *    zero-task agents vanish) and mislabels status 5 (Approved) as "InProgress"
 *    and status 6 (Updated) as "Blocked", meaning real Blocked (4) is counted
 *    nowhere. Workload comes from GET /admin/staff/workload instead, which has
 *    correct status mapping and a LEFT JOIN.
 *
 *  - /expiry-alerts/documents and /expiry-alerts/employees are not wrapped.
 *    They are server-capped at 20 rows, split entity- and employee-owned
 *    documents across two calls, and return no owner names. The
 *    /admin/documents/expiring registry supersedes them.
 */

/** GET /api/v1/dashboard/summary */
export async function getDashboardSummary() {
  const response = await apiClient.get('/dashboard/summary')
  return response.data.data
}

/** GET /api/v1/dashboard/activity-feed — Query: page (1), pageSize (20) */
export async function getDashboardActivityFeed(params = {}) {
  const response = await apiClient.get('/dashboard/activity-feed', { params })
  return response.data.data
}

/**
 * GET /api/v1/dashboard/revenue — Query: period ("month" | "week")
 * NOT CALLED BY THE PHASE 1 DASHBOARD (approved decision: no revenue figures).
 * Retained for a later approved phase. Only `period === "week"` is honoured; any
 * other value falls back to 1 month. Counts Paid invoices only.
 */
export async function getDashboardRevenue(params = {}) {
  const response = await apiClient.get('/dashboard/revenue', { params })
  return response.data.data
}
