import { KeyRound, Mail, ShieldCheck, User } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import { accountStatuses, emailLabel, roleLabel } from './settingsDisplay'

function SummaryTile({ icon: Icon, label, children }) {
  return (
    <div className="rounded-[10px] border border-[#E2E4E9] bg-white p-4">
      <div className="mb-2 flex items-center gap-2 text-xs font-medium uppercase tracking-wider text-[#6B7280]">
        {Icon ? <Icon size={12} strokeWidth={2} aria-hidden="true" /> : null}
        {label}
      </div>
      {children}
    </div>
  )
}

/**
 * Read-only account facts for the signed-in Admin.
 *
 * Email, Role, account state and email verification are the only four facts the
 * profile DTO can honestly provide. Email and Role are immutable, so they are
 * shown as labels and never as form controls.
 */
function AdminProfileSummary({ profile }) {
  if (!profile) {
    return null
  }

  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
      <SummaryTile icon={Mail} label="Email">
        <p className="break-all text-sm font-medium text-[#16181D]">
          {emailLabel(profile.email)}
        </p>
      </SummaryTile>

      <SummaryTile icon={User} label="Role">
        <div className="flex flex-wrap items-center gap-2">
          <StatusPill label={roleLabel(profile.role)} tone="success" />
        </div>
      </SummaryTile>

      <SummaryTile icon={ShieldCheck} label="Account">
        <div className="flex flex-wrap items-center gap-2">
          {accountStatuses(profile).map((status) => (
            <StatusPill key={status.key} label={status.label} tone={status.tone} />
          ))}
        </div>
      </SummaryTile>

      <SummaryTile icon={KeyRound} label="User ID">
        <p className="break-all font-mono text-xs text-[#6B7280]">{profile.id}</p>
      </SummaryTile>
    </div>
  )
}

export default AdminProfileSummary
