import { ListChecks } from 'lucide-react'
import AdminClientSection from '../clients/AdminClientSection'
import { staffWorkloadRows, staffWorkloadTotal, toCount } from './staffDisplay'

/**
 * One agent's task workload, from GET /api/v1/admin/staff/{staffId}/workload.
 *
 * This section is the first consumer of the per-agent workload endpoint. The
 * aggregate GET /admin/staff/workload is already the dashboard's single source
 * under its own key and is NOT called here: fetching every agent's rows to draw
 * one person's numbers would be a much heavier query for a strictly smaller
 * answer.
 *
 * THE SIX-STATUS SPLIT IS THE FIGURE THIS SECTION BELIEVES. `activeTaskCount`
 * and `blockedTaskCount` are shown, but the stated total is the sum of the six
 * status rows and nothing else. Those are different sets — activeTaskCount
 * repeats the backend's 1,2,3,5 definition, which omits Blocked and Updated —
 * so adding it to the six rows would produce a total that contradicts the rows
 * printed directly above it. The dashboard makes the same choice for the same
 * reason.
 *
 * EVERY STATUS IS RENDERED, INCLUDING THE ZEROS. The backend populates all six
 * keys, but a missing one is shown as 0 rather than dropped, so the number of
 * rows never depends on the response and the rows can be compared like for like.
 *
 * NO DERIVED PERCENTAGES, no "busiest agent", no ranking. This is one agent's
 * counts; any comparison across agents belongs to the aggregate endpoint and the
 * dashboard that already owns it.
 *
 * ZERO TASKS IS AN EMPTY STATE, NOT A ZERO. An agent with no assigned work is a
 * real, successful answer, and it is presented as such rather than as a table of
 * six zeros.
 */
function AdminStaffWorkloadSection({ workload, loading, error, onRetry }) {
  const rows = staffWorkloadRows(workload)
  const total = staffWorkloadTotal(workload)
  const activeCount = toCount(workload?.activeTaskCount)
  const blockedCount = toCount(workload?.blockedTaskCount)

  return (
    <AdminClientSection
      title="Task workload"
      description="Assigned renewal tasks for this agent, split by status."
      loading={loading}
      loadingLabel="Loading workload…"
      error={error}
      onRetry={onRetry}
      errorMessage="Could not load this agent's workload."
      isEmpty={!loading && !error && total === 0}
      emptyMessage="No assigned tasks."
      emptyDescription="This agent currently has no renewal tasks assigned to them."
      emptyIcon={ListChecks}
    >
      <div className="flex flex-col gap-4">
        {/*
          Zero is rendered from the response and never derived from a failed or
          missing request: `error` above takes priority, so these numbers are only
          ever reached when the request succeeded.
        */}
        <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-3">
          <WorkloadTile label="Active tasks" value={activeCount} />
          <WorkloadTile label="Blocked tasks" value={blockedCount} />
          <WorkloadTile label="Total by status" value={total} emphasis />
        </div>

        <div className="overflow-x-auto">
          <table className="w-full border-collapse text-left text-sm">
            <caption className="sr-only">
              Assigned renewal tasks for this agent, by status
            </caption>
            <thead>
              <tr className="border-b border-[#E2E4E9]">
                <th
                  scope="col"
                  className="px-3 py-2 text-xs font-semibold uppercase tracking-wider text-[#6B7280] first:pl-0"
                >
                  Status
                </th>
                <th
                  scope="col"
                  className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wider text-[#6B7280] last:pr-0"
                >
                  Tasks
                </th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.key} className="border-b border-[#E2E4E9] last:border-b-0">
                  <td className="px-3 py-2.5 align-middle text-[#16181D] first:pl-0">
                    {row.label}
                  </td>
                  <td className="px-3 py-2.5 text-right align-middle tabular-nums text-[#16181D] last:pr-0">
                    {row.count}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <p className="text-xs text-[#6B7280]">
          Only tasks assigned to this agent are counted. An unassigned task appears
          in no agent&apos;s figures.
        </p>
      </div>
    </AdminClientSection>
  )
}

function WorkloadTile({ label, value, emphasis = false }) {
  return (
    <div
      className={`rounded-[10px] border px-3.5 py-3 ${
        emphasis
          ? 'border-[#0F9D74]/25 bg-[rgba(15,157,116,0.06)]'
          : 'border-[#E2E4E9] bg-[#F7F8FA]/60'
      }`}
    >
      <p className="text-xs font-medium text-[#6B7280]">{label}</p>
      <p
        className={`mt-1 text-lg font-semibold tabular-nums ${
          emphasis ? 'text-[#0B7A5A]' : 'text-[#16181D]'
        }`}
      >
        {value}
      </p>
    </div>
  )
}

export default AdminStaffWorkloadSection
