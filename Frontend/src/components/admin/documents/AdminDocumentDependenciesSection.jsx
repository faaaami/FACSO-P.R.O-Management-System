import { Link2 } from 'lucide-react'
import AdminClientSection, {
  AdminClientRecordCard,
  AdminClientRecordList,
} from '../clients/AdminClientSection'
import { useAdminDocumentDependencies } from '../../../hooks/admin/useAdminDocuments'
import { displayText, formatDate } from '../clients/clientDisplay'

/**
 * Documents that the current document depends on — read-only.
 *
 * BACKING QUERY: GET /api/v1/admin/documents/{documentId}/dependencies
 * Response: { documentId, items, totalCount } — UNPAGINATED. The action binds no
 * query parameters, so there is no page selector and `totalCount` is a plain row
 * count. Unlike the contract list, this DTO really does carry a total, so it is
 * displayed.
 *
 * FIELDS ARE EXACTLY THOSE ON DocumentDependencyItemDto: id, dependsOnDocumentId,
 * documentNumber, expiryDate, isActive, isDeleted. There is no title, owner, or
 * file name on the item, so the row shows the document number and nothing more.
 * A blank expiryDate is normal — the column is nullable — and is rendered as
 * presentText's dash rather than a fabricated date.
 *
 * `isActive` and `isDeleted` describe the DEPENDENCY EDGE, not the target
 * document, so they are shown as recorded and not interpreted as a judgement
 * about whether the other document is still usable.
 *
 * The list is rendered in the order the backend returns it. Dependencies can
 * form a cycle, and nothing here defines a traversal order, so the rows are not
 * sorted, de-duplicated, or labelled as topologically ordered.
 *
 * This is display only. Adding or removing a dependency is a mutation and lives
 * on the documents mutation surface, not here.
 */
function AdminDocumentDependenciesSection({ documentId }) {
  const { items, totalCount, isLoading, isError, error, isFetching, refresh } =
    useAdminDocumentDependencies(documentId)

  const toolbar =
    !isLoading && !isError && items.length > 0 ? (
      <p className="text-xs text-[#6B7280]">
        Showing all {totalCount.toLocaleString('en-US')}{' '}
        {totalCount === 1 ? 'dependency' : 'dependencies'}. This endpoint does not
        paginate, so there is no page selector.
      </p>
    ) : null

  return (
    <AdminClientSection
      title="Dependencies"
      description="Other documents that this one relies on."
      toolbar={toolbar}
      loading={isLoading}
      loadingLabel="Loading dependencies…"
      error={isError ? error : null}
      onRetry={() => refresh()}
      errorMessage="Could not load this document's dependencies."
      isEmpty={!isLoading && !isError && items.length === 0}
      emptyMessage="No dependencies recorded."
      emptyDescription="Nothing is linked to this document. A dependency appears here when this document is recorded as relying on another one."
      emptyIcon={Link2}
    >
      <div className="flex flex-col gap-3">
        {isFetching && !isLoading && (
          <p className="text-xs text-[#6B7280]" role="status">
            Refreshing dependencies…
          </p>
        )}

        <AdminClientRecordList items={items}>
          {(dependency) => (
            <AdminClientRecordCard
              key={dependency?.id}
              title={displayText(dependency?.documentNumber)}
              subtitle="Document depended on"
              meta={[
                { label: 'Expires', value: formatDate(dependency?.expiryDate) },
                { label: 'Edge active', value: dependency?.isActive ? 'Yes' : 'No' },
                { label: 'Edge deleted', value: dependency?.isDeleted ? 'Yes' : 'No' },
              ]}
            />
          )}
        </AdminClientRecordList>
      </div>
    </AdminClientSection>
  )
}

export default AdminDocumentDependenciesSection
