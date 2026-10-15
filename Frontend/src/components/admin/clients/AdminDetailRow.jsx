import { MISSING_VALUE } from './clientDisplay'

/**
 * One label/value line inside an Admin detail panel.
 *
 * The Admin drawer repeats this shape in the Overview section, the entity detail
 * view and anywhere else a verified field is shown, so it is a component rather
 * than a local function copied into each of them — five hand-rolled copies drift
 * apart within a phase, and the drift is invisible until the columns misalign.
 *
 * A missing value renders an em dash, which is a legitimate "this field is
 * empty". It is never used for a failed request: a section that failed renders an
 * ErrorState with a retry instead, so a dash can always be trusted as a real
 * absence rather than a disguised error.
 */
function AdminDetailRow({ label, value, mono = false }) {
  const resolved = value ?? MISSING_VALUE

  return (
    <div className="grid grid-cols-1 gap-0.5 border-b border-[#E2E4E9] py-2.5 last:border-b-0 sm:grid-cols-[minmax(0,10rem)_minmax(0,1fr)] sm:gap-4">
      <dt className="text-xs font-medium text-[#6B7280]">{label}</dt>
      <dd
        className={`min-w-0 text-sm text-[#16181D] ${
          resolved === MISSING_VALUE ? 'text-[#9CA3AF]' : ''
        } ${mono && resolved !== MISSING_VALUE ? 'font-mono text-[13px]' : ''} break-words`}
      >
        {resolved}
      </dd>
    </div>
  )
}

export default AdminDetailRow
