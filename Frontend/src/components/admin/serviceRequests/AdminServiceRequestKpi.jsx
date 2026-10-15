import { ClipboardList } from 'lucide-react'

/**
 * The one truthful figure on the Admin Service Requests page.
 *
 * WHY THERE IS EXACTLY ONE CARD. The list endpoint is
 * GET /api/v1/service-requests?page&pageSize and accepts no status, type, company,
 * date or text filter, so per-status counts cannot be produced. There is also no
 * service-request figure in GET /api/v1/dashboard/summary — DashboardSummaryDto
 * carries TotalClients, TotalEntities, TotalEmployees, ActiveRenewalTasks,
 * PendingRetainerInvoices and PendingServiceFeeInvoices, and nothing about
 * requests — and no aggregate endpoint exists anywhere.
 *
 * The Client portal's Service Requests page shows four cards because
 * GET /api/v1/client/service-requests DOES accept a status filter, so it can ask
 * the same endpoint four times for four authoritative counts. That capability is
 * not available to Admin, so that component is not reusable here and the numbers
 * behind it are not reproducible. Deriving per-status counts by paging every
 * request in the system and tallying client-side was rejected outright: it is an
 * unbounded walk whose cost grows with totalCount, and the result would be a
 * client-side guess presented with a server's authority.
 *
 * list.totalCount IS authoritative, and it is unfiltered — the SQL counts
 * service_requests where is_deleted = false with no other predicate. So it is the
 * same number the list-card badge shows, and both read the one query's totalCount
 * so they cannot disagree.
 *
 * A FAILED OR IN-FLIGHT REQUEST NEVER READS AS ZERO. totalCount falls back to 0 in
 * both states, and a card built on that would be a confident wrong number at the
 * two moments that matter most, so the figure is withheld and shown as an
 * explicit dash marked Unavailable — the rule DashboardKpiCard already exists to
 * enforce, applied here to the single figure this module is allowed to show.
 */
function AdminServiceRequestKpi({ totalCount, isLoading, isError, isFetching }) {
  const available = !isLoading && !isError

  return (
    <section aria-labelledby="admin-service-request-kpi-heading" className="mb-5">
      <div className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)] sm:p-5">
        <div className="flex items-start gap-3">
          <span
            className="flex h-10 w-10 shrink-0 items-center justify-center rounded-[8px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] text-[#0F9D74]"
            aria-hidden="true"
          >
            <ClipboardList size={18} strokeWidth={1.75} />
          </span>

          <div className="min-w-0 flex-1">
            <h2
              id="admin-service-request-kpi-heading"
              className="text-xs font-semibold uppercase tracking-wider text-[#6B7280]"
            >
              Total service requests
            </h2>

            {isLoading ? (
              <>
                <div className="mt-2.5 w-20 animate-pulse rounded-[6px] bg-[#F7F8FA]" style={{ height: 28 }} aria-hidden="true" />
                <p className="mt-1.5 text-xs text-[#9CA3AF]">Loading…</p>
              </>
            ) : available ? (
              <>
                <p className="mt-1 text-3xl font-bold leading-none tracking-tight tabular-nums text-[#16181D]">
                  {totalCount.toLocaleString('en-US')}
                  <span className="sr-only"> service requests in total</span>
                </p>
                <p className="mt-1.5 text-xs text-[#9CA3AF]">
                  {isFetching ? 'Updating…' : 'Unfiltered'}
                </p>
              </>
            ) : (
              <>
                <p className="mt-1 text-3xl font-bold leading-none tracking-tight text-[#9CA3AF]">
                  &mdash;
                  <span className="sr-only"> total service request count unavailable</span>
                </p>
                <p className="mt-1.5 text-xs text-[#9CA3AF]">Unavailable</p>
              </>
            )}
          </div>
        </div>

        <p className="mt-3 flex items-start gap-2 border-t border-[#E2E4E9] pt-3 text-xs text-[#6B7280]">
          <span aria-hidden="true" className="mt-px shrink-0">
            &bull;
          </span>
          <span>
            This is the unfiltered total of every service request on record. The
            backend exposes no per-status counts for requests, so no Submitted,
            Converted or Rejected figure is shown here; the list below is not
            filtered either, because the same endpoint accepts no filter.
          </span>
        </p>
      </div>
    </section>
  )
}

export default AdminServiceRequestKpi
