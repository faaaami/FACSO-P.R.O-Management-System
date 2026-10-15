import { Building2, Pencil, Trash2 } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import AdminDetailRow from './AdminDetailRow'
import AdminClientSection from './AdminClientSection'
import {
  displayText,
  formatDateTime,
  presentText,
  recordStatusLabel,
  recordStatusTone,
} from './clientDisplay'

/**
 * Everything the company detail DTO actually carries, grouped so the registration
 * facts are readable without scrolling past the contact block.
 *
 * FIELDS ARE THE VERIFIED ONES. GetClientCompanyByIdResponseDto is
 * { id, companyName, tradeLicenseNumber, phone, email, address, emirate, isActive,
 * isDeleted, createdAt, updatedAt }. Address and updatedAt exist here and NOT on
 * the list DTO, which is why the list has no address column.
 *
 * There is no status field beyond isActive/isDeleted, no approved/pending state,
 * no deletedAt and no aggregate counts, so none are shown. The id is presented
 * because it is genuinely useful when correlating a record with a support
 * request, and it is the only handle this module has on the record.
 *
 * The two actions sit here because this is the tab that describes the company
 * record, and both act on that record rather than on a child. Neither is offered
 * for a soft-deleted company: the detail view is the one place a deleted company
 * is still readable (its nested routes 404), and offering an edit there would
 * invite a change to a record that cannot then be verified through the UI.
 */
function AdminClientOverviewSection({ client, onEdit, onDelete }) {
  const isDeleted = Boolean(client?.isDeleted)

  return (
    <AdminClientSection
      title="Company details"
      description="Registered details held for this client company."
      action={
        isDeleted ? null : (
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={onEdit}
              className="inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
            >
              <Pencil size={13} strokeWidth={2} aria-hidden="true" />
              Edit
              <span className="sr-only"> company details</span>
            </button>
            <button
              type="button"
              onClick={onDelete}
              className="inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-red-200 bg-white px-2.5 py-1.5 text-xs font-semibold text-[#DC2626] transition duration-150 hover:bg-red-50 focus:outline-none focus:ring-2 focus:ring-red-100"
            >
              <Trash2 size={13} strokeWidth={2} aria-hidden="true" />
              Delete
              <span className="sr-only"> company</span>
            </button>
          </div>
        )
      }
    >
      <div className="flex flex-col gap-5">
        <div>
          <h4 className="mb-1 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
            Registration
          </h4>
          <dl className="flex flex-col">
            <AdminDetailRow label="Company name" value={displayText(client?.companyName)} />
            <AdminDetailRow
              label="Trade licence"
              value={displayText(client?.tradeLicenseNumber)}
              mono
            />
            <AdminDetailRow label="Emirate" value={displayText(client?.emirate)} />
            <AdminDetailRow label="Address" value={displayText(client?.address)} />
            <AdminDetailRow
              label="Status"
              value={
                <StatusPill
                  label={recordStatusLabel(client)}
                  tone={recordStatusTone(client)}
                />
              }
            />
          </dl>
        </div>

        <div>
          <h4 className="mb-1 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
            Contact
          </h4>
          <dl className="flex flex-col">
            <AdminDetailRow label="Phone" value={displayText(client?.phone)} />
            <AdminDetailRow label="Email" value={displayText(client?.email)} />
          </dl>
        </div>

        <div>
          <h4 className="mb-1 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
            Record
          </h4>
          <dl className="flex flex-col">
            <AdminDetailRow label="Client ID" value={client?.id ?? '—'} mono />
            <AdminDetailRow label="Created" value={formatDateTime(client?.createdAt)} />
            <AdminDetailRow label="Last updated" value={formatDateTime(client?.updatedAt)} />
          </dl>
        </div>

        {client?.isDeleted && (
          <p className="rounded-[10px] border border-amber-200 bg-amber-50/60 px-3.5 py-2.5 text-xs text-[#92400E]">
            This company is soft-deleted. Its legal entities and contacts are not
            reachable, because the backend scopes those routes to companies that
            are not deleted.
          </p>
        )}

        {!presentText(client?.companyName) && (
          <p className="flex items-start gap-2 text-xs text-[#6B7280]">
            <Building2 size={14} className="mt-0.5 shrink-0" aria-hidden="true" />
            This company record has no name on file.
          </p>
        )}
      </div>
    </AdminClientSection>
  )
}

export default AdminClientOverviewSection
