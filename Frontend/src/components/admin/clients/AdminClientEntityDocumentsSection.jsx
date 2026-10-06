import { FileText } from 'lucide-react'
import AdminClientSection, {
  AdminClientRecordCard,
  AdminClientRecordList,
} from './AdminClientSection'
import StatusPill from '../../client/StatusPill'
import { useAdminEntityDocuments } from '../../../hooks/admin/useAdminDocuments'
import { displayText, formatDate, formatDateTime } from './clientDisplay'
import {
  documentTypeLabel,
  formatFileSize,
  storedStatusLabel,
  storedStatusTone,
} from '../documents/documentDisplay'

/**
 * Documents held against one legal entity — read-only.
 *
 * BACKING QUERY: GET /api/v1/admin/entities/{entityId}/documents
 * Response: { items, totalCount } — UNPAGINATED. The action binds only
 * `includeDeleted`, so there is no page selector and `totalCount` is a plain row
 * count. A note says so, so the absent pager does not read as a defect.
 *
 * FIELDS ARE EXACTLY THOSE ON ClientEntityDocumentListItemDto: id, clientEntityId,
 * type, documentNumber, issueDate, expiryDate, fileUrl, fileName, contentType,
 * fileSize, isActive, isDeleted, createdAt, updatedAt, status.
 *
 * `status` is the DocumentStatus ENUM as the backend computed it, so the pill
 * uses the stored-status label and tone helpers. The expiry registry returns a
 * different plain-string vocabulary for the same concept; the two are not
 * reconciled in the browser, and this section deliberately never recomputes a
 * status from `expiryDate` the way it must not be recomputed from
 * `daysRemaining` on the registry.
 *
 * Issue and expiry dates are both nullable and are rendered through formatDate,
 * which yields null for absent or unparseable input rather than an invented date.
 *
 * This is display only. Attaching or detaching a document from an entity is a
 * mutation and is not offered here.
 */
function AdminClientEntityDocumentsSection({ entityId }) {
  const { items, totalCount, isLoading, isError, error, isFetching, refresh } =
    useAdminEntityDocuments(entityId)

  const toolbar =
    !isLoading && !isError && items.length > 0 ? (
      <p className="text-xs text-[#6B7280]">
        Showing all {totalCount.toLocaleString('en-US')}{' '}
        {totalCount === 1 ? 'document' : 'documents'} on this entity. This endpoint
        does not paginate, so there is no page selector.
      </p>
    ) : null

  return (
    <AdminClientSection
      title="Documents"
      description="Documents held against this legal entity."
      toolbar={toolbar}
      loading={isLoading}
      loadingLabel="Loading entity documents…"
      error={isError ? error : null}
      onRetry={() => refresh()}
      errorMessage="Could not load this entity's documents."
      isEmpty={!isLoading && !isError && items.length === 0}
      emptyMessage="No documents on file."
      emptyDescription="This legal entity has no documents recorded against it. Documents for a company are attached to an individual entity, so a company with several entities may hold documents on only some of them."
      emptyIcon={FileText}
    >
      <div className="flex flex-col gap-3">
        {isFetching && !isLoading && (
          <p className="text-xs text-[#6B7280]" role="status">
            Refreshing entity documents…
          </p>
        )}

        <AdminClientRecordList items={items}>
          {(document) => (
            <AdminClientRecordCard
              key={document?.id}
              title={displayText(document?.documentNumber)}
              subtitle={
                document?.fileName
                  ? `${documentTypeLabel(document?.type) ?? 'Unknown type'} · ${document.fileName}`
                  : (documentTypeLabel(document?.type) ?? 'Unknown type')
              }
              trailing={
                <StatusPill
                  label={storedStatusLabel(document?.status) ?? 'Unknown'}
                  tone={storedStatusTone(document?.status)}
                />
              }
              meta={[
                { label: 'Issued', value: formatDate(document?.issueDate) ?? displayText(null) },
                { label: 'Expires', value: formatDate(document?.expiryDate) ?? displayText(null) },
                { label: 'File size', value: formatFileSize(document?.fileSize) },
                { label: 'Active', value: document?.isActive ? 'Yes' : 'No' },
                { label: 'Deleted', value: document?.isDeleted ? 'Yes' : 'No' },
                { label: 'Last updated', value: formatDateTime(document?.updatedAt) },
              ]}
            />
          )}
        </AdminClientRecordList>
      </div>
    </AdminClientSection>
  )
}

export default AdminClientEntityDocumentsSection
