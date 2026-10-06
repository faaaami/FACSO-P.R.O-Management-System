import { useEffect, useId, useState } from 'react'
import { AlertTriangle, Loader2, Pencil, UserRound, UserX, X } from 'lucide-react'
import { toast } from 'sonner'
import StatusPill from '../../client/StatusPill'
import ErrorState from '../../client/ErrorState'
import LoadingState from '../../client/LoadingState'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import { useAdminStaffMember, useAdminStaffWorkload } from '../../../hooks/admin/useAdminStaff'
import { useDeactivateAdminStaff } from '../../../hooks/admin/useAdminStaffMutations'
import { extractApiErrorMessage } from '../../../utils/apiError'
import AdminStaffOverviewSection from './AdminStaffOverviewSection'
import AdminStaffWorkloadSection from './AdminStaffWorkloadSection'
import { openTaskCountForDeactivation, staffName, staffActiveLabel, staffActiveTone } from './staffDisplay'

/**
 * Admin staff detail drawer.
 *
 * Built on the shared useFocusTrap exactly as the Admin Employees drawer is, so
 * Escape, bidirectional Tab wrap, focus-on-open, focus restoration to the
 * triggering control and body scroll lock all behave the same across the Admin
 * surface. The hook is used unmodified; overlay click-to-close is wired here
 * because the hook does not provide it.
 *
 * TWO SECTIONS, TWO INDEPENDENT REQUESTS, AND AN ASYMMETRIC FAILURE POLICY.
 *
 * Overview comes first because the header needs the name. If it fails there is
 * no coherent panel above the sections, so it becomes a full-drawer error with a
 * retry — the same rule the Employees drawer uses. Workload is fetched
 * independently and fails independently: a workload error leaves the profile
 * fully readable, which matters because the deactivation gate below depends on
 * workload data and must therefore be able to show "we do not know" without
 * destroying what the user came to read.
 *
 * 404 IS HANDLED SPECIFICALLY. Both GET /admin/staff/{id} and
 * GET /admin/staff/{id}/workload 404 for an id that is not an Agent or no longer
 * exists, so a generic failure message would be misleading — this is not a flaky
 * network, it is a record that cannot be addressed any more.
 *
 * ONE PANEL, ONE FOCUS TRAP. The edit and deactivate dialogs opened from here
 * register themselves on top of this surface in useFocusTrap's modal stack, so
 * Escape closes the dialog first and leaves the drawer open.
 */
function AdminStaffDetailDrawer({ staffId, onClose, onEdit, onDeactivate }) {
  const titleId = useId()
  const panelRef = useFocusTrap({ isOpen: true, onClose })

  const {
    data: staff,
    isLoading,
    isError,
    error,
    refresh,
  } = useAdminStaffMember(staffId)

  const {
    data: workload,
    isLoading: workloadLoading,
    isError: workloadIsError,
    error: workloadError,
    refresh: refreshWorkload,
  } = useAdminStaffWorkload(staffId)

  const name = staffName(staff)
  const notFound = error?.response?.status === 404

  return (
    <div className="fixed inset-0 z-50">
      <div
        className="fixed inset-0 bg-slate-900/40"
        onClick={onClose}
        aria-hidden="true"
      />

      <aside
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        className="fixed inset-y-0 right-0 z-50 flex w-full max-w-[680px] flex-col border-l border-[#E2E4E9] bg-white shadow-[0_8px_24px_rgba(28,31,38,0.10)] focus:outline-none"
      >
        <div className="flex items-start justify-between gap-4 border-b border-[#E2E4E9] p-5 sm:p-6">
          <div className="flex min-w-0 items-start gap-3">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] border border-[#E2E4E9] bg-[#F7F8FA] text-[#16181D]">
              <UserRound size={18} strokeWidth={1.75} aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <h2
                id={titleId}
                className="break-words text-base font-semibold tracking-tight text-[#16181D]"
              >
                {isLoading && !staff ? 'Loading staff…' : name}
              </h2>
              {staff && (
                <div className="mt-1.5 flex flex-wrap items-center gap-2">
                  <StatusPill
                    label={staffActiveLabel(staff)}
                    tone={staffActiveTone(staff)}
                  />
                  <span className="min-w-0 break-words text-xs text-[#6B7280]">
                    {staff?.email}
                  </span>
                </div>
              )}
            </div>
          </div>

          <button
            type="button"
            onClick={onClose}
            aria-label={`Close ${name} details`}
            className="shrink-0 cursor-pointer rounded-[8px] p-1.5 text-[#6B7280] transition duration-150 hover:bg-gray-100 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74]"
          >
            <X className="h-5 w-5" aria-hidden="true" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-5 sm:p-6">
          {isLoading && <LoadingState label="Loading staff…" />}

          {isError && (
            <ErrorState
              message={
                notFound
                  ? 'This staff record was not found. It may have been removed, or it is no longer an Agent account.'
                  : extractApiErrorMessage(error, 'Could not load this staff record.')
              }
              onRetry={() => refresh()}
            />
          )}

          {!isLoading && !isError && staff && (
            <div className="flex flex-col gap-7">
              <AdminStaffOverviewSection staff={staff} />

              <div className="border-t border-[#E2E4E9] pt-6">
                <AdminStaffWorkloadSection
                  workload={workload}
                  loading={workloadLoading}
                  error={workloadIsError ? workloadError : null}
                  onRetry={() => refreshWorkload()}
                />
              </div>
            </div>
          )}
        </div>

        {!isLoading && !isError && staff && (
          <div className="flex flex-wrap items-center justify-end gap-2 border-t border-[#E2E4E9] p-5 sm:p-6">
            {staff?.isActive ? (
              <button
                type="button"
                onClick={() => onDeactivate(staff)}
                className="inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] border border-[#DC2626]/25 bg-white px-3.5 py-2 text-xs font-semibold text-[#B91C1C] transition duration-150 hover:bg-red-50 focus:outline-none focus:ring-2 focus:ring-[rgba(220,38,38,0.15)]"
              >
                <UserX size={14} strokeWidth={2} aria-hidden="true" />
                Deactivate
                <span className="sr-only"> {name}</span>
              </button>
            ) : null}

            <button
              type="button"
              onClick={() => onEdit(staff)}
              className="inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3.5 py-2 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
            >
              <Pencil size={14} strokeWidth={2} aria-hidden="true" />
              Edit
              <span className="sr-only"> {name}</span>
            </button>
          </div>
        )}
      </aside>
    </div>
  )
}

/**
 * Irreversible-deactivation confirmation.
 *
 * EXPORTED FROM THIS FILE rather than given its own, because the approved file
 * list for this phase does not include one and the alternative — a ninth file —
 * would be a small abstraction bought at the cost of a stated constraint. The
 * page imports it as a named export for the row-action path; the drawer imports
 * it implicitly by calling back up to the page, so both entry points reach the
 * same dialog and therefore the same safety gate.
 *
 * THE SAFETY GATE IS A FRONTEND-ONLY REFUSAL, and that is the point.
 * DeactivateStaffCommandHandler sets IsActive = false and saves without ever
 * looking at the agent's tasks. A request would therefore succeed and strand
 * every open renewal task on an account that can never be switched back on. So
 * the count is established first, from GET /admin/staff/{id}/workload, and the
 * action is refused while it is non-zero.
 *
 * AN UNKNOWN COUNT IS NOT A ZERO COUNT. While the workload is loading, or if it
 * failed, the button stays disabled and the dialog says the count could not be
 * established. Treating "we do not know" as "there is nothing open" would let the
 * exact deactivation this guard exists to prevent through on a failed request —
 * and a user watching a spinner would have no way to tell the two apart.
 *
 * EXACT-NAME CONFIRMATION, NOT A YES/NO. The typed value is compared against
 * the record's own fullName with no trimming and no case folding, so it has to
 * match what the server holds. The requirement is stated above the field, wired
 * to it with aria-describedby, and the button's accessible name repeats it, so
 * the constraint is available to a screen reader rather than being a visual
 * puzzle.
 *
 * The dialog states plainly that this cannot be undone. It does not mention
 * restoring the account, because no endpoint exists that could do it.
 */
function AdminStaffDeactivateDialog({ staff, onClose, onDeactivated }) {
  const titleId = useId()
  const confirmHintId = useId()
  const panelRef = useFocusTrap({ isOpen: true, onClose })

  const [typedName, setTypedName] = useState('')
  const [errorMessage, setErrorMessage] = useState(null)

  const {
    data: workload,
    isLoading,
    isError,
    error,
    refresh,
  } = useAdminStaffWorkload(staff?.id)

  const deactivate = useDeactivateAdminStaff()

  const name = staffName(staff)
  const openCount = openTaskCountForDeactivation(workload)
  const countKnown = !isLoading && !isError && openCount !== null
  const hasOpenWork = countKnown && openCount > 0
  const nameMatches = typedName === staff?.fullName

  // A fresh dialog must not open pre-filled, or the confirmation step would be
  // satisfied by the component that rendered it.
  useEffect(() => {
    setTypedName('')
    setErrorMessage(null)
  }, [staff?.id])

  function handleSubmit(event) {
    event.preventDefault()
    setErrorMessage(null)

    deactivate.mutate(staff?.id, {
      onSuccess: () => {
        toast.success(`${name} deactivated.`)
        onDeactivated?.()
        onClose()
      },
      onError: (requestError) => {
        // Already-inactive arrives as 400 from the handler's own guard.
        const message = extractApiErrorMessage(
          requestError,
          'Could not deactivate this staff member.',
        )
        setErrorMessage(message)
        toast.error(message)
      },
    })
  }

  const blocked = !countKnown || hasOpenWork || !nameMatches || deactivate.isPending

  return (
    <div className="fixed inset-0 z-50">
      <div
        className="fixed inset-0 bg-slate-900/40"
        onClick={onClose}
        aria-hidden="true"
      />

      <div className="relative flex min-h-full items-end justify-center sm:items-center sm:p-6">
        <div
          ref={panelRef}
          role="dialog"
          aria-modal="true"
          aria-labelledby={titleId}
          tabIndex={-1}
          className="relative w-full max-w-lg rounded-t-[14px] border border-[#E2E4E9] bg-white p-5 shadow-[0_8px_24px_rgba(28,31,38,0.10)] focus:outline-none sm:rounded-[14px] sm:p-6"
        >
          <div className="flex items-start gap-3">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-red-100 text-[#DC2626]">
              <AlertTriangle size={17} strokeWidth={2} aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <h2
                id={titleId}
                className="text-base font-semibold tracking-tight text-[#16181D]"
              >
                Deactivate {name}
              </h2>
              <p className="mt-1 text-xs text-[#6B7280]">
                This sets the account inactive and removes them from the default
                staff list.{' '}
                <span className="font-semibold text-[#B91C1C]">This cannot be undone.</span>
              </p>
            </div>
          </div>

          {/*
            The workload is the safety gate, so it is resolved in the dialog rather
            than assumed from whatever the drawer happened to have loaded. A row
            action opens this without a drawer, and a cached count from a previous
            visit would be a stale basis for an irreversible decision.
          */}
          <div className="mt-5">
            {isLoading && <LoadingState label="Checking assigned tasks…" />}

            {isError && (
              <ErrorState
                message={
                  error?.response?.status === 404
                    ? 'Could not establish the assigned task count, so this action is blocked. This staff record was not found, which means it is no longer an Agent account or no longer exists.'
                    : 'Could not establish the assigned task count, so this action is blocked. Retry to check the assigned work before deactivating.'
                }
                onRetry={() => refresh()}
              />
            )}

            {countKnown && hasOpenWork && (
              <div
                role="alert"
                className="rounded-[10px] border border-red-200 bg-red-50/60 px-4 py-3 text-xs text-[#B91C1C]"
              >
                <p className="font-semibold">Deactivation is blocked.</p>
                <p className="mt-1">
                  {name} still has {openCount} open assigned{' '}
                  {openCount === 1 ? 'task' : 'tasks'}. The server does not check
                  this, so it would accept the request and leave those tasks on an
                  account that cannot be switched back on. Reassign the work
                  first.
                </p>
              </div>
            )}

            {countKnown && !hasOpenWork && (
              <p className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 px-4 py-3 text-xs text-[#6B7280]">
                {name} has no open assigned tasks, so nothing is left behind by
                deactivating this account.
              </p>
            )}
          </div>

          {errorMessage && (
            <div
              role="alert"
              className="mt-4 flex items-center gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-4 py-2.5"
            >
              <p className="text-xs font-medium text-[#DC2626]">{errorMessage}</p>
            </div>
          )}

          <form onSubmit={handleSubmit} noValidate className="mt-5 flex flex-col gap-4">
            <div>
              <label
                htmlFor="adminStaffDeactivateConfirm"
                className="mb-1.5 block text-sm font-medium text-[#16181D]"
              >
                Type <span className="font-semibold">{staff?.fullName}</span> to confirm
              </label>
              <input
                id="adminStaffDeactivateConfirm"
                type="text"
                value={typedName}
                onChange={(event) => setTypedName(event.target.value)}
                autoComplete="off"
                disabled={deactivate.isPending || hasOpenWork}
                aria-describedby={confirmHintId}
                className="block w-full rounded-[10px] border border-[#E2E4E9] bg-white px-3.5 py-2.5 text-sm text-[#16181D] placeholder:text-[#9CA3AF] focus:border-[#0F9D74] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] disabled:bg-gray-50 disabled:text-[#9CA3AF]"
              />
              <p id={confirmHintId} className="mt-1.5 text-xs text-[#6B7280]">
                The exact name as shown, including capitalisation. The button stays
                disabled until it matches.
              </p>
            </div>

            <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
              <button
                type="button"
                onClick={onClose}
                disabled={deactivate.isPending}
                className="inline-flex cursor-pointer items-center justify-center rounded-[10px] border border-[#E2E4E9] bg-white px-4 py-2.5 text-sm font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={blocked}
                className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-[10px] bg-[#DC2626] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:bg-[#B91C1C] focus:outline-none focus:ring-2 focus:ring-[#DC2626] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {deactivate.isPending ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                    <span>Deactivating…</span>
                  </>
                ) : (
                  <>
                    <span>Deactivate {name}</span>
                    <span className="sr-only"> — this cannot be undone</span>
                  </>
                )}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  )
}

export { AdminStaffDeactivateDialog }
export default AdminStaffDetailDrawer
