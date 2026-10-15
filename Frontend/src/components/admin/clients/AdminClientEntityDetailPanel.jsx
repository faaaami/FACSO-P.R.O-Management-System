import { ArrowLeft, Pencil, Trash2 } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import AdminClientSection from './AdminClientSection'
import AdminClientEntityDocumentsSection from './AdminClientEntityDocumentsSection'
import AdminDetailRow from './AdminDetailRow'
import { useAdminClientEntity } from '../../../hooks/admin/useAdminClients'
import {
  displayText,
  formatDateTime,
  recordStatusLabel,
  recordStatusTone,
} from './clientDisplay'

/**
 * Detail view for one legal entity, shown INSIDE the drawer rather than as a
 * second drawer.
 *
 * A nested 448px panel inside a 680px panel leaves very little of the page
 * visible, and stacking two modal surfaces makes it ambiguous which one Escape
 * will close. This replaces the drawer body instead: the section switcher stays
 * put, there is still exactly one focus trap, and Back is always in the same
 * place. The entity list remains mounted underneath, so returning is instant and
 * its scroll position and page number are preserved.
 *
 * Backing query GET /api/v1/admin/clients/{clientId}/entities/{entityId}
 * This adds only updatedAt over the list row. There is no address, email, phone,
 * establishment card or licence expiry date on this DTO, so none is shown.
 *
 * A second section below renders the entity's documents from their own endpoint
 * and their own query, so this panel's own record and that list can fail
 * separately.
 *
 * EDIT AND DELETE LIVE HERE, not on the list row, because both act on a record
 * with more than the three fields a list row shows. Editing from the row would
 * either open a form missing the fields the user cannot see, or require the panel
 * to be open anyway — in which case the row button is a second path to the same
 * place. Neither control is offered for a soft-deleted entity: the detail GET
 * takes includeDeleted but this panel never asks for it, so a deleted entity is
 * not reachable here at all.
 */
function AdminClientEntityDetailPanel({ clientId, entityId, entityName, onBack, onEdit, onDelete }) {
  const { data: entity, isLoading, isError, error, refresh } = useAdminClientEntity(
    clientId,
    entityId,
  )

  return (
    <div>
      <button
        type="button"
        onClick={onBack}
        className="mb-4 inline-flex items-center gap-1.5 rounded-[8px] px-2 py-1 text-xs font-semibold text-[#0F9D74] transition duration-150 hover:bg-[rgba(15,157,116,0.08)] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer"
      >
        <ArrowLeft size={14} strokeWidth={2} aria-hidden="true" />
        Back to legal entities
      </button>

      <AdminClientSection
        title={entity?.entityName ?? entityName ?? 'Legal entity'}
        description="Entity record as held for this company."
        action={
          entity?.isDeleted ? null : (
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => onEdit(entity ?? { id: entityId, entityName })}
                className="inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
              >
                <Pencil size={13} strokeWidth={2} aria-hidden="true" />
                Edit
                <span className="sr-only"> this entity</span>
              </button>
              <button
                type="button"
                onClick={() => onDelete(entity ?? { id: entityId, entityName })}
                className="inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-red-200 bg-white px-2.5 py-1.5 text-xs font-semibold text-[#DC2626] transition duration-150 hover:bg-red-50 focus:outline-none focus:ring-2 focus:ring-red-100"
              >
                <Trash2 size={13} strokeWidth={2} aria-hidden="true" />
                Delete
                <span className="sr-only"> this entity</span>
              </button>
            </div>
          )
        }
        loading={isLoading}
        loadingLabel="Loading entity…"
        error={isError ? error : null}
        onRetry={() => refresh()}
        errorMessage="Could not load this legal entity."
      >
        <dl className="flex flex-col">
          <AdminDetailRow label="Entity name" value={displayText(entity?.entityName)} />
          <AdminDetailRow
            label="Trade licence"
            value={displayText(entity?.tradeLicenseNumber)}
            mono
          />
          <AdminDetailRow label="Emirate" value={displayText(entity?.emirate)} />
          <AdminDetailRow
            label="Status"
            value={
              <StatusPill
                label={recordStatusLabel(entity)}
                tone={recordStatusTone(entity)}
              />
            }
          />
          <AdminDetailRow label="Entity ID" value={entity?.id ?? '—'} mono />
          <AdminDetailRow label="Created" value={formatDateTime(entity?.createdAt)} />
          <AdminDetailRow label="Last updated" value={formatDateTime(entity?.updatedAt)} />
        </dl>
      </AdminClientSection>

      {/*
        A second, independent section rather than more rows above. It runs its own
        query, so a failure here leaves the entity record readable above it — the
        same reason the drawer's other sections each own their state. The entity
        key is deliberately untouched: this list has a different key, so nothing
        here can invalidate the entity detail already in cache.
      */}
      <div className="mt-6">
        <AdminClientEntityDocumentsSection entityId={entityId} />
      </div>
    </div>
  )
}

export default AdminClientEntityDetailPanel
