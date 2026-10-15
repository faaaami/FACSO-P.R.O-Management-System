import { Link } from 'react-router-dom'
import { RotateCcw } from 'lucide-react'

/**
 * A single dashboard KPI.
 *
 * The one rule this component exists to enforce: a failed request must never look
 * like a measurement. A genuine zero renders as `0`. A request that failed or is
 * still in flight renders an em dash plus an explicit word, and offers a retry.
 * Collapsing those two states into "0" is how an operations dashboard starts
 * lying to its readers, so they are visually and textually distinct here.
 *
 * When `href` is supplied the whole card becomes a real <Link>, so it is
 * keyboard reachable and announced as a link. The unavailable/loading states
 * deliberately render a plain <div> instead, because nesting the retry button
 * inside a link would produce invalid, ambiguous interactive markup.
 */
function DashboardKpiCard({
  label,
  value,
  context,
  icon: Icon,
  href,
  unavailable = false,
  loading = false,
  onRetry,
}) {
  // A null value is treated exactly like a failure. If the response arrives but
  // the field is missing, rendering nothing would look like a blank measurement,
  // which is the same lie as showing 0.
  const unavailableState = loading || unavailable || value == null

  const body = (
    <>
      <div className="flex items-start justify-between gap-3">
        <p className="text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
          {label}
        </p>
        {Icon && (
          <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] text-[#6B7280]">
            <Icon size={14} strokeWidth={1.75} aria-hidden="true" />
          </span>
        )}
      </div>

      {loading ? (
        <div className="mt-3" aria-hidden="true">
          <div className="h-8 w-16 animate-pulse rounded-[6px] bg-[#F7F8FA]" />
          <div className="mt-2.5 h-3 w-28 animate-pulse rounded-[6px] bg-[#F7F8FA]" />
        </div>
      ) : unavailable ? (
        <div className="mt-2" role="status">
          <p className="text-3xl font-bold leading-none tracking-tight text-[#9CA3AF]">&mdash;</p>
          <p className="mt-2 text-xs font-medium text-[#6B7280]">Unavailable</p>
          {onRetry && (
            <button
              type="button"
              onClick={onRetry}
              className="mt-2 inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-2.5 py-1 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
            >
              <RotateCcw size={12} strokeWidth={2} aria-hidden="true" />
              Retry
            </button>
          )}
        </div>
      ) : (
        <>
          <p className="mt-2.5 text-3xl font-bold leading-none tracking-tight text-[#16181D]">
            {typeof value === 'number' ? value.toLocaleString('en-US') : value}
          </p>
          {context && <p className="mt-2 text-xs text-[#6B7280]">{context}</p>}
        </>
      )}

      {unavailableState && <span className="sr-only">Loading {label}</span>}
    </>
  )

  const className =
    'block rounded-[12px] border border-[#E2E4E9] bg-white p-5 shadow-[0_1px_3px_rgba(28,31,38,0.06)]'

  if (href && !unavailableState) {
    return (
      <Link
        to={href}
        className={`${className} transition duration-150 hover:border-[#0F9D74]/40 hover:bg-[#F7F8FA]/60 focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]`}
      >
        {body}
        <span className="sr-only"> — open module</span>
      </Link>
    )
  }

  return <div className={className}>{body}</div>
}

export default DashboardKpiCard
