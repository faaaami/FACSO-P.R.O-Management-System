import { Construction } from 'lucide-react'
import AdminPageHeader from './AdminPageHeader'

/**
 * Phase 0 shell for an Admin module whose functionality is scheduled for a later
 * phase.
 *
 * It deliberately renders NO data, makes NO request and offers NO controls: a
 * module that looks interactive but is not would be worse than one that openly
 * says it is not built yet. The `sources` list documents the verified backend
 * endpoints the module will use, so the contract is recorded before any code
 * depends on it.
 */
function AdminPhaseShell({ title, subtitle, sources }) {
  return (
    <div className="space-y-6">
      <AdminPageHeader title={title} subtitle={subtitle} />

      <section
        className="rounded-[12px] border border-[#E2E4E9] bg-white p-6 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
        aria-labelledby={`${title.replace(/\s+/g, '-').toLowerCase()}-phase-heading`}
      >
        <div className="flex items-start gap-3">
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] border border-[#E2E4E9] bg-[#F7F8FA] text-[#6B7280]">
            <Construction size={18} strokeWidth={1.75} aria-hidden="true" />
          </span>
          <div className="min-w-0 flex-1">
            <h2
              id={`${title.replace(/\s+/g, '-').toLowerCase()}-phase-heading`}
              className="text-sm font-semibold text-[#16181D]"
            >
              Not built yet
            </h2>
            <p className="mt-1 text-sm text-[#6B7280]">
              This module is scheduled for a later phase. The Admin foundation —
              routing, page structure, API layer, query hooks and enum labels — is
              in place, so no data is shown here until the module is built against
              the endpoints below.
            </p>

            {sources?.length > 0 && (
              <div className="mt-4">
                <p className="text-xs font-semibold uppercase tracking-wider text-[#9CA3AF]">
                  Verified data sources
                </p>
                <ul className="mt-2 space-y-1.5">
                  {sources.map((source) => (
                    <li
                      key={source}
                      className="rounded-[8px] border border-[#E2E4E9] bg-[#F7F8FA] px-3 py-1.5 font-mono text-xs text-[#16181D]"
                    >
                      {source}
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        </div>
      </section>
    </div>
  )
}

export default AdminPhaseShell
