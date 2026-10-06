import { useId } from 'react'
import { Link } from 'react-router-dom'
import { ArrowRight } from 'lucide-react'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import EmptyState from '../../client/EmptyState'
import { extractApiErrorMessage } from '../../../utils/apiError'

/**
 * Common section frame for every Phase 1 Admin Dashboard widget.
 *
 * A widget owns exactly one concern: the header (title, context subtitle, and an
 * optional "View all" link into the module that owns the data) plus a body that
 * resolves to one of four honest states — loading, error, empty, or content.
 *
 * The state contract is the point of this component:
 *   - a failed request renders ErrorState with a retry, NEVER a zero or a dash
 *     that could be mistaken for a real measurement
 *   - an empty result renders EmptyState, which is a legitimate outcome
 *   - each widget is a <section> with an <h2>, so a screen-reader user can jump
 *     between dashboard regions the way they jump between headings
 *
 * It is deliberately not a generic Card: the body is passed in rather than
 * reimplemented here, because the four Phase 1 widgets have genuinely different
 * bodies (a proportional bar, a data list, a KPI grid).
 */
function DashboardWidget({
  title,
  subtitle,
  icon: Icon,
  actionTo,
  actionLabel = 'View all',
  loading = false,
  loadingLabel = 'Loading…',
  error = null,
  onRetry,
  errorMessage = 'Could not load this section.',
  isEmpty = false,
  emptyMessage = 'Nothing to show yet.',
  emptyDescription,
  emptyIcon,
  children,
}) {
  const id = useId()
  const headingId = `${id}-heading`

  function renderBody() {
    if (loading) return <LoadingState label={loadingLabel} />
    if (error) {
      return (
        <ErrorState
          message={extractApiErrorMessage(error, errorMessage)}
          onRetry={onRetry}
        />
      )
    }
    if (isEmpty) {
      return <EmptyState message={emptyMessage} description={emptyDescription} icon={emptyIcon} />
    }
    return children
  }

  return (
    <section
      aria-labelledby={headingId}
      className="flex flex-col rounded-[12px] border border-[#E2E4E9] bg-white p-6 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
    >
      <div className="mb-5 flex items-start justify-between gap-4">
        <div className="flex min-w-0 items-start gap-3">
          {Icon && (
            <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] border border-[#E2E4E9] bg-[#F7F8FA] text-[#16181D]">
              <Icon size={18} strokeWidth={1.75} aria-hidden="true" />
            </span>
          )}
          <div className="min-w-0">
            <h2 id={headingId} className="text-base font-semibold tracking-tight text-[#16181D]">
              {title}
            </h2>
            {subtitle && <p className="mt-0.5 text-xs text-[#6B7280]">{subtitle}</p>}
          </div>
        </div>

        {actionTo && (
          <Link
            to={actionTo}
            className="inline-flex shrink-0 items-center gap-1 rounded-[10px] px-2 py-1 text-xs font-semibold text-[#0F9D74] transition duration-150 hover:bg-[rgba(15,157,116,0.08)] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
          >
            {actionLabel}
            <ArrowRight size={13} strokeWidth={2} aria-hidden="true" />
            <span className="sr-only"> {title}</span>
          </Link>
        )}
      </div>

      <div className="flex-1">{renderBody()}</div>
    </section>
  )
}

export default DashboardWidget
