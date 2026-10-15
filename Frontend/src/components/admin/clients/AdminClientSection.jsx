import { useId } from 'react'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import EmptyState from '../../client/EmptyState'
import { extractApiErrorMessage } from '../../../utils/apiError'

/**
 * One section of the Admin client drawer, with the state contract every section
 * shares.
 *
 * The contract is the point of this component. Each of the five data sections
 * can fail independently, and each must resolve to exactly one of four honest
 * outcomes:
 *
 *   loading -> a spinner, because the answer is still coming
 *   error   -> a message plus a retry, NEVER a dash, a zero or an empty list
 *   empty   -> a real empty response, which is a legitimate outcome
 *   content -> the rows
 *
 * The failure case is the one that is easy to get wrong. A section that returned
 * nothing because the request failed and a section that genuinely has no
 * contacts look identical if the error branch is missing, and the second one
 * quietly reports "this company has no contacts" when the truth is "we do not
 * know". That is why the error branch comes before the empty branch and why it
 * is not allowed to degrade into a blank.
 *
 * Counts are never rendered here. A count is derived from `totalCount`, which
 * falls back to 0 while loading and on failure, so any badge built on it would
 * be a fabricated measurement in exactly the two cases that matter most. The
 * caller decides what to show once it knows the request succeeded.
 *
 * `toolbar` sits between the heading and the body so a section filter stays
 * reachable when the body is empty or has failed. Without that slot a user who
 * searched too narrowly would be shown an empty state with no way to clear the
 * search, because the clear button would have been inside the discarded body.
 *
 * `action` and `emptyAction` are the two onboarding affordances, and they are
 * kept apart on purpose. `action` sits in the heading row and is for a section
 * that has rows; `emptyAction` renders under the empty state and is for a section
 * that has none. A section offers one or the other, never both, because showing
 * "Add entity" twice — once in the header of an empty section and once in its
 * body — would put the same button in two places on the same screen for no gain.
 * Both are optional, so the read-only sections that pass neither are unchanged.
 */
function AdminClientSection({ title, description, toolbar, action, emptyAction, loading = false, loadingLabel, error = null, onRetry, errorMessage, isEmpty = false, emptyMessage, emptyDescription, emptyIcon, children }) {
  const id = useId()
  const headingId = `${id}-heading`

  function renderBody() {
    if (loading) return <LoadingState label={loadingLabel ?? 'Loading…'} />
    if (error) {
      return <ErrorState message={extractApiErrorMessage(error, errorMessage ?? 'Could not load this section.')} onRetry={onRetry} />
    }
    if (isEmpty) {
      return (
        <>
          <EmptyState message={emptyMessage ?? 'Nothing to show yet.'} description={emptyDescription} icon={emptyIcon} />
          {emptyAction && <div className="mt-3 flex justify-center">{emptyAction}</div>}
        </>
      )
    }
    return children
  }

  return (
    <section aria-labelledby={headingId} className="flex flex-col">
      <div className="mb-4 flex items-start justify-between gap-3">
        <div className="min-w-0">
          <h3 id={headingId} className="text-sm font-semibold tracking-tight text-[#16181D]">
            {title}
          </h3>
          {description && <p className="mt-0.5 text-xs text-[#6B7280]">{description}</p>}
        </div>
        {action && <div className="shrink-0">{action}</div>}
      </div>
      {toolbar && <div className="mb-4">{toolbar}</div>}
      {renderBody()}
    </section>
  )
}

/**
 * Compact record list used inside the drawer for entities, contacts, contracts
 * and employees.
 *
 * These are lists of short records inside a ~680px panel, not data tables, so
 * each row is a labelled block rather than a row of columns. A table here would
 * either overflow the panel or need horizontal scrolling inside a drawer that
 * already has a vertical scroll of its own.
 *
 * `meta` is an optional array of { label, value } pairs rendered as a
 * definition list, so a screen reader announces which field each value belongs
 * to instead of hearing a run of unlabelled strings.
 */
export function AdminClientRecordList({ items, children }) {
  return <ul className="flex flex-col gap-2.5">{items.map(children)}</ul>
}

/**
 * One row of an AdminClientRecordList. `meta` renders as label/value pairs.
 *
 * `actions` is an optional slot for controls that act on this row specifically
 * — approving a contact, deactivating one, deleting one. It sits under the meta
 * list and before the `onOpen` button, so a row that opens a detail view keeps
 * its "view" affordance in the same place it has always been. The alternative,
 * cramming several controls into the trailing pill slot next to the status, would
 * have put destructive actions where a status label lives.
 */
export function AdminClientRecordCard({ title, subtitle, meta = [], trailing, actions, onOpen, openLabel }) {
  return (
    <li className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 p-3.5">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="break-words text-sm font-semibold text-[#16181D]">{title}</p>
          {subtitle && <p className="mt-0.5 break-words text-xs text-[#6B7280]">{subtitle}</p>}
        </div>
        {trailing && <div className="shrink-0">{trailing}</div>}
      </div>

      {meta.length > 0 && (
        <dl className="mt-2.5 flex flex-col gap-1.5">
          {meta.map((entry) => (
            <div key={entry.label} className="flex items-start justify-between gap-3">
              <dt className="text-xs text-[#6B7280]">{entry.label}</dt>
              <dd className="min-w-0 break-words text-right text-xs font-medium text-[#16181D]">
                {entry.value}
              </dd>
            </div>
          ))}
        </dl>
      )}

      {actions && <div className="mt-3 flex flex-wrap items-center gap-2">{actions}</div>}

      {onOpen && (
        <button
          type="button"
          onClick={onOpen}
          className="mt-3 inline-flex items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-white focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer"
        >
          {openLabel ?? 'View details'}
          {openLabel && <span className="sr-only"> for {title}</span>}
        </button>
      )}
    </li>
  )
}

export default AdminClientSection
