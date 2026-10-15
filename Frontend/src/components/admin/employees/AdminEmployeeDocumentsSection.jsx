import { useCallback, useState } from 'react'
import { FileText } from 'lucide-react'
import AdminClientSection, {
  AdminClientRecordCard,
  AdminClientRecordList,
} from '../clients/AdminClientSection'
import StatusPill from '../../client/StatusPill'
import DocumentFilePanel from '../../documents/DocumentFilePanel'
import { documentStatusLabel } from '../enumLabels'
import { getAdminDocumentFile } from '../../../api/admin/documents'
import { useAdminEmployeeDocuments } from '../../../hooks/admin/useAdminEmployees'
import { displayText, formatDate, presentText } from '../clients/clientDisplay'
import {
  daysRemainingText,
  documentStatusTone,
  documentTypeLabel,
} from './employeeDisplay'

/**
 * Documents held against one employee.
 *
 * BACKING QUERY: GET /api/v1/admin/employees/{employeeId}/documents
 * Response: { items, totalCount }
 *
 * THIS ENDPOINT IS NOT PAGINATED. The controller action binds only
 * `includeDeleted` — there is no page or pageSize parameter and neither appears
 * in the response — so no pager is rendered here. totalCount is a plain row count
 * and is shown as a count, with a note explaining the missing page selector so
 * its absence does not read as a defect.
 *
 * STATUS VOCABULARY IS TAKEN VERBATIM FROM THIS ENDPOINT. `status` is the
 * DocumentStatus enum, string-serialized and computed server-side by
 * SqlSnippets.DocumentStatusNumericCase against a 90-day window, so the values
 * are Active / ExpiringSoon / Overdue. (InRenewal exists in the enum but this
 * query never produces it.) The pill is rendered through the shared
 * documentStatusLabel / documentStatusTone, which are keyed on exactly that enum.
 *
 * The expiring endpoints are deferred out of this phase and speak a DIFFERENT
 * vocabulary — plain labels Active / ExpiringSoon / Expired. "Overdue" is
 * therefore never relabelled "Expired", and no client-side expiry window is
 * computed to second-guess the server. daysRemainingText contributes a neutral
 * arithmetic distance only, so every colour on screen traces back to a field the
 * backend actually sent.
 *
 * FILE ACCESS GOES THROUGH THE AUTHORIZED FILE ENDPOINT. Employee documents are
 * rows in the shared `documents` table keyed by employee_id, so the shared
 * getAdminDocumentFile (GET /admin/documents/{id}/file) resolves the bytes for
 * them; that wrapper is reused rather than duplicated here. The action is offered
 * only on rows that carry a fileUrl, because the endpoint 404s a document with no
 * associated file — showing the button there would manufacture an error. The
 * fileUrl is used ONLY as that presence check: it is a relative storage key, not a
 * retrievable URL, so it is never displayed and never navigated to.
 *
 * Opening a row selects it and loads its file through the shared DocumentFilePanel
 * below the list, which turns the bytes into a short-lived object URL. This
 * replaced a pre-opened popup that was navigated to the stored path: opening a
 * window and then fetching is what that pattern was working around, and it left a
 * blank tab behind whenever the fetch failed.
 */
function AdminEmployeeDocumentsSection({ employeeId }) {
  const {
    items,
    totalCount,
    isLoading,
    isError,
    error,
    isFetching,
    refresh,
  } = useAdminEmployeeDocuments(employeeId)

  const [selectedDocumentId, setSelectedDocumentId] = useState(null)

  const selectedDocument = items.find((item) => item?.id === selectedDocumentId) ?? null
  const loadSelectedFile = useCallback(
    () => (selectedDocumentId ? getAdminDocumentFile(selectedDocumentId) : null),
    [selectedDocumentId],
  )

  const viewableCount = items.filter((document) =>
    presentText(document?.fileUrl),
  ).length

  const toolbar =
    !isLoading && !isError && items.length > 0 ? (
      <p className="text-xs text-[#6B7280]">
        Showing all {totalCount.toLocaleString('en-US')}{' '}
        {totalCount === 1 ? 'document' : 'documents'}. This endpoint does not
        paginate, so there is no page selector.
      </p>
    ) : null

  return (
    <AdminClientSection
      title="Documents"
      description="Identity and immigration documents recorded against this employee."
      toolbar={toolbar}
      loading={isLoading}
      loadingLabel="Loading documents…"
      error={isError ? error : null}
      onRetry={() => refresh()}
      errorMessage="Could not load this employee's documents."
      isEmpty={!isLoading && !isError && items.length === 0}
      emptyMessage="No documents on file."
      emptyDescription="Documents uploaded against this employee will appear here."
      emptyIcon={FileText}
    >
      <div className="flex flex-col gap-3">
        {isFetching && !isLoading && (
          <p className="text-xs text-[#6B7280]" role="status">
            Refreshing documents…
          </p>
        )}

        <AdminClientRecordList items={items}>
          {(document) => {
            const type = documentTypeLabel(document?.type) ?? 'Document'
            const number = presentText(document?.documentNumber)
            const fileName = presentText(document?.fileName)
            const days = daysRemainingText(document?.expiryDate)
            const canOpen = Boolean(presentText(document?.fileUrl))
            const title = number ? `${type} · ${number}` : type

            return (
              <AdminClientRecordCard
                key={document?.id}
                title={title}
                subtitle={fileName ?? undefined}
                trailing={
                  <StatusPill
                    label={documentStatusLabel(document?.status) ?? 'Unknown'}
                    tone={documentStatusTone(document?.status)}
                  />
                }
                meta={[
                  { label: 'Type', value: type },
                  {
                    label: 'Document no.',
                    value: displayText(document?.documentNumber),
                  },
                  { label: 'Issue date', value: formatDate(document?.issueDate) },
                  { label: 'Expiry date', value: formatDate(document?.expiryDate) },
                  // Neutral arithmetic only. The status pill above is the
                  // authoritative state, straight from the backend.
                  ...(days ? [{ label: 'Timing', value: days }] : []),
                ]}
                onOpen={canOpen ? () => setSelectedDocumentId(document.id) : undefined}
                openLabel={canOpen ? 'View file' : undefined}
              />
            )
          }}
        </AdminClientRecordList>

        {viewableCount === 0 && items.length > 0 && (
          <p className="text-xs text-[#6B7280]">
            No document here has an associated file, so there is nothing to open.
          </p>
        )}

        {/*
          One file panel for the selected row, rather than a panel inside every
          card. Opening a document fetches its bytes, and fetching all of them for
          a list nobody asked for would pull every file in the employee's record
          over the wire.

          The bytes come from GET /admin/documents/{id}/file, which authorizes the
          admin and streams the content. The stored `fileUrl` still gates whether
          the action is offered — it is a reliable "is there a file" signal — but
          it is never displayed or navigated to.
        */}
        {selectedDocument ? (
          <DocumentFilePanel
            load={loadSelectedFile}
            identity={['admin-document', selectedDocumentId]}
            fileName={selectedDocument.fileName}
            contentType={selectedDocument.contentType}
            label={`View ${documentTypeLabel(selectedDocument?.type) ?? 'the file'}`}
          />
        ) : null}
      </div>
    </AdminClientSection>
  )
}

export default AdminEmployeeDocumentsSection
