import { History } from 'lucide-react'
import AdminClientSection, {
  AdminClientRecordCard,
  AdminClientRecordList,
} from '../clients/AdminClientSection'
import { useAdminDocumentVersions } from '../../../hooks/admin/useAdminDocuments'
import { displayText, formatDateTime } from '../clients/clientDisplay'
import { formatFileSize } from './documentDisplay'

/**
 * Stored file versions for one document.
 *
 * BACKING QUERY: GET /api/v1/admin/documents/{documentId}/versions
 * Response: { items, totalCount } — UNPAGINATED.
 *
 * The controller action binds only `includeDeleted`; there is no page or pageSize
 * and neither appears in the response, so no pager is rendered here and totalCount
 * is a plain row count. A note says so explicitly, so the missing page selector does
 * not read as a defect.
 *
 * FIELDS ARE EXACTLY THOSE ON DocumentVersionListItemDto: id, documentId,
 * versionNumber, fileUrl, fileName, contentType, fileSize, isDeleted, createdAt,
 * updatedAt. `versionNumber` is supplied by the backend and is shown.
 *
 * What this DTO does NOT carry, and therefore what is not shown: an uploader or
 * author, and any "is current" flag. The document's own record is the current
 * version, but inferring that from list position would be a guess, so no row is
 * marked as current. No version is opened from here either — the versions route
 * hands back a raw fileUrl, while the supported way to open a file is the shared
 * scan route, which is already offered once in the Overview tab. Duplicating the
 * popup-and-fallback machinery per version row would triple that code for no new
 * capability.
 */
function AdminDocumentVersionsSection({ documentId }) {
  const { items, totalCount, isLoading, isError, error, isFetching, refresh } =
    useAdminDocumentVersions(documentId)

  const toolbar =
    !isLoading && !isError && items.length > 0 ? (
      <p className="text-xs text-[#6B7280]">
        Showing all {totalCount.toLocaleString('en-US')}{' '}
        {totalCount === 1 ? 'version' : 'versions'}. This endpoint does not paginate,
        so there is no page selector.
      </p>
    ) : null

  return (
    <AdminClientSection
      title="Versions"
      description="Files previously stored against this document."
      toolbar={toolbar}
      loading={isLoading}
      loadingLabel="Loading versions…"
      error={isError ? error : null}
      onRetry={() => refresh()}
      errorMessage="Could not load this document's versions."
      isEmpty={!isLoading && !isError && items.length === 0}
      emptyMessage="No stored versions."
      emptyDescription="Only the original document is on file. A version appears here once a replacement file has been stored against it."
      emptyIcon={History}
    >
      <div className="flex flex-col gap-3">
        {isFetching && !isLoading && (
          <p className="text-xs text-[#6B7280]" role="status">
            Refreshing versions…
          </p>
        )}

        <AdminClientRecordList items={items}>
          {(version) => (
            <AdminClientRecordCard
              key={version?.id}
              title={`Version ${version?.versionNumber ?? displayText(null)}`}
              subtitle={displayText(version?.fileName)}
              meta={[
                {
                  label: 'Content type',
                  value: displayText(version?.contentType),
                },
                { label: 'File size', value: formatFileSize(version?.fileSize) },
                { label: 'Stored', value: formatDateTime(version?.createdAt) },
                { label: 'Last updated', value: formatDateTime(version?.updatedAt) },
                {
                  label: 'Deleted',
                  value: version?.isDeleted ? 'Yes' : 'No',
                },
              ]}
            />
          )}
        </AdminClientRecordList>
      </div>
    </AdminClientSection>
  )
}

export default AdminDocumentVersionsSection
