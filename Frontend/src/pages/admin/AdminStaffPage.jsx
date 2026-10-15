import { useEffect, useMemo, useState } from 'react'
import { ShieldCheck, UserPlus, Users } from 'lucide-react'
import AdminPageHeader from '../../components/admin/AdminPageHeader'
import SectionCard from '../../components/client/SectionCard'
import LoadingState from '../../components/client/LoadingState'
import ErrorState from '../../components/client/ErrorState'
import EmptyState from '../../components/client/EmptyState'
import Pagination from '../../components/client/billing/Pagination'
import AdminStaffFilterBar from '../../components/admin/staff/AdminStaffFilterBar'
import AdminStaffTable from '../../components/admin/staff/AdminStaffTable'
import AdminStaffDetailDrawer, {
  AdminStaffDeactivateDialog,
} from '../../components/admin/staff/AdminStaffDetailDrawer'
import AdminStaffFormDialog from '../../components/admin/staff/AdminStaffFormDialog'
import { useAdminStaff } from '../../hooks/admin/useAdminStaff'
import { extractApiErrorMessage } from '../../utils/apiError'

/**
 * Admin Portal staff directory — Phase 2, full CRUD-within-limits.
 *
 * BACKING QUERIES
 *   GET  /api/v1/admin/staff?includeInactive&page&pageSize   the list
 *   GET  /api/v1/admin/staff/{staffId}                      the drawer profile
 *   GET  /api/v1/admin/staff/{staffId}/workload             the drawer workload
 *   POST /api/v1/admin/staff                                create
 *   PATCH /api/v1/admin/staff/{staffId}                     update (full replace)
 *   DELETE /api/v1/admin/staff/{staffId}                    deactivate (no body)
 *
 * AGENT-ONLY DATASET. Every staff handler filters `role = Agent`, so there is no
 * role filter here, no role control on the create form, and no way to reach an
 * Admin account from this page. Role is displayed and nothing more.
 *
 * WHY THE SEARCH IS CURRENT-PAGE ONLY. The list endpoint binds includeInactive,
 * page and pageSize and nothing else — no search parameter, and no text predicate
 * in the SQL. Filtering therefore happens in the browser over the rows already
 * loaded. The filter bar, the live match count and the empty state each say so,
 * because a box that silently misses a matching agent on page 3 is worse than no
 * box at all. Note the deliberate consequence: changing the search does NOT reset
 * to page 1, since the filter belongs to the page you are looking at.
 *
 * WHY A NEWLY DEACTIVATED LAST ROW STEPS THE PAGE BACK. The list hides inactive
 * rows unless includeInactive is on, so deactivating the only row on the final
 * page leaves that page genuinely empty while the previous one still has data.
 * Rather than special-casing the deactivate callback, the clamp watches for "a
 * page beyond the first that came back empty while rows still exist" and steps
 * back one page at a time. That covers the case regardless of which control
 * caused it, and it cannot loop: it stops at page 1, and it does nothing when
 * totalCount is 0 because then there is genuinely nothing to go back to.
 *
 * WHY THE COUNT BADGE IS SUPPRESSED WHILE LOADING OR AFTER A FAILURE. totalCount
 * falls back to 0 in both cases, so a badge built on it would print a fabricated
 * "0" in exactly the two situations where the number means most.
 *
 * There is no sort control: the query hard-orders by created_at DESC, id ASC, so
 * no column can be made sortable and no header may look clickable.
 */
const PAGE_SIZE = 20

const ADMIN_PORTAL_BADGE = (
  <span className="inline-flex items-center gap-1.5 rounded-[6px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74]">
    <ShieldCheck size={13} strokeWidth={2} aria-hidden="true" />
    Admin Portal
  </span>
)

/** Case-insensitive contains over fullName and email. Nothing else is searched. */
function matchesSearch(staff, needle) {
  if (!needle) return true
  const term = needle.toLowerCase()
  const name = typeof staff?.fullName === 'string' ? staff.fullName.toLowerCase() : ''
  const email = typeof staff?.email === 'string' ? staff.email.toLowerCase() : ''
  return name.includes(term) || email.includes(term)
}

function AdminStaffDirectory() {
  const [includeInactive, setIncludeInactive] = useState(false)
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [selectedStaffId, setSelectedStaffId] = useState(null)
  const [editingStaff, setEditingStaff] = useState(null)
  const [deactivatingStaff, setDeactivatingStaff] = useState(null)
  const [isCreateOpen, setIsCreateOpen] = useState(false)

  const { items, totalCount, isLoading, isError, error, isFetching, refresh } =
    useAdminStaff({ includeInactive, page, pageSize: PAGE_SIZE })

  // A new includeInactive value is a different result set, so the current page
  // number is meaningless against it and can point past its end.
  useEffect(() => {
    setPage(1)
  }, [includeInactive])

  /*
   * The final-page clamp described in the file header. Every condition is load
   * bearing: without the loading and error guards it would fire against a stale
   * empty array during a refetch, and without the totalCount guard it would chase
   * page 1 when the directory is genuinely empty.
   */
  useEffect(() => {
    if (isLoading || isError) return
    if (items.length > 0) return
    if (page <= 1) return
    if (totalCount === 0) return
    setPage(page - 1)
  }, [isLoading, isError, items, page, totalCount])

  const visibleItems = useMemo(
    () => items.filter((staff) => matchesSearch(staff, search.trim())),
    [items, search],
  )

  const hasSearch = search.trim().length > 0
  const showCountBadge = !isLoading && !isError

  let content
  if (isLoading) {
    content = <LoadingState label="Loading staff…" />
  } else if (isError) {
    // A failed list is an error with a retry. It is never an empty list and never
    // a total of zero, because either would read as a real measurement.
    content = (
      <ErrorState
        message={extractApiErrorMessage(error, 'Could not load staff.')}
        onRetry={() => refresh()}
      />
    )
  } else if (items.length === 0) {
    // Distinguish "the directory is empty" from "everything here is hidden
    // because it is inactive", which is what includeInactive=false produces.
    content = (
      <EmptyState
        icon={Users}
        message={
          includeInactive ? 'No staff accounts yet.' : 'No active staff accounts.'
        }
        description={
          includeInactive
            ? 'Staff accounts will appear here once they are created.'
            : 'Every staff account on file is inactive. Turn on “Include inactive” to see them.'
        }
      />
    )
  } else if (hasSearch && visibleItems.length === 0) {
    // The backend DID return rows — they are simply on another page. Saying
    // "no staff found" here would be a false negative about the whole directory.
    content = (
      <EmptyState
        icon={Users}
        message="No match on this page."
        description="This box searches the current page only. Try another page, or clear the filter to see all loaded rows."
      />
    )
  } else {
    content = (
      <AdminStaffTable
        items={visibleItems}
        onOpenDetails={setSelectedStaffId}
        onEdit={setEditingStaff}
        onDeactivate={setDeactivatingStaff}
      />
    )
  }

  return (
    <>
      <SectionCard
        title="Staff"
        icon={Users}
        subtitle="Firm staff accounts, newest records first. Every account here has the Agent role."
        badge={
          showCountBadge ? (
            <span className="rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold tabular-nums text-[#6B7280]">
              {totalCount.toLocaleString('en-US')}
              <span className="sr-only"> staff accounts</span>
            </span>
          ) : null
        }
        action={
          <button
            type="button"
            onClick={() => setIsCreateOpen(true)}
            className="inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] bg-[#1C1F26] px-3.5 py-2 text-xs font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2"
          >
            <UserPlus size={14} strokeWidth={2} aria-hidden="true" />
            Create staff
          </button>
        }
      >
        <AdminStaffFilterBar
          search={search}
          onSearchChange={setSearch}
          includeInactive={includeInactive}
          onIncludeInactiveChange={setIncludeInactive}
          isFetching={isFetching && !isLoading}
          matchCount={visibleItems.length}
          pageRowCount={items.length}
        />

        {content}

        {showCountBadge && totalCount > PAGE_SIZE && (
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={totalCount}
            itemLabel="staff account"
            itemLabelPlural="staff accounts"
            onPageChange={setPage}
          />
        )}
      </SectionCard>

      {selectedStaffId && (
        <AdminStaffDetailDrawer
          staffId={selectedStaffId}
          onClose={() => setSelectedStaffId(null)}
          onEdit={(staff) => setEditingStaff(staff)}
          onDeactivate={(staff) => setDeactivatingStaff(staff)}
        />
      )}

      {editingStaff && (
        <AdminStaffFormDialog
          mode="edit"
          staff={editingStaff}
          onClose={() => setEditingStaff(null)}
        />
      )}

      {isCreateOpen && (
        <AdminStaffFormDialog mode="create" onClose={() => setIsCreateOpen(false)} />
      )}

      {deactivatingStaff && (
        <AdminStaffDeactivateDialog
          staff={deactivatingStaff}
          onClose={() => setDeactivatingStaff(null)}
          onDeactivated={() => {
            // The drawer is showing a row that is now inactive, and the clamp
            // above handles the list. Closing it avoids a panel whose header
            // contradicts the record it was opened for.
            setSelectedStaffId(null)
          }}
        />
      )}
    </>
  )
}

function AdminStaffPage() {
  return (
    <div className="space-y-6">
      <AdminPageHeader
        title="Staff"
        subtitle="Firm staff accounts and their assigned task workload. Deactivating an account cannot be undone."
        action={ADMIN_PORTAL_BADGE}
      />
      <AdminStaffDirectory />
    </div>
  )
}

export default AdminStaffPage
