import { useCallback } from 'react'
import AdminClientSection from '../clients/AdminClientSection'
import AdminDetailRow from '../clients/AdminDetailRow'
import StatusPill from '../../client/StatusPill'
import DocumentFilePanel from '../../documents/DocumentFilePanel'
import { getAdminDocumentFile } from '../../../api/admin/documents'
import { displayText, formatDate, formatDateTime, presentText } from '../clients/clientDisplay'
import {
  detailsJsonText,
  documentTypeLabel,
  formatFileSize,
  storedStatusLabel,
  storedStatusTone,
} from './documentDisplay'

/**
 * Document record: the detail fields, plus the file action.
 *
 * BACKING QUERY: GET /api/v1/admin/documents/{documentId}?includeDeleted
 * Response: GetDocumentByIdResponseDto
 *
 * FIELDS ARE EXACTLY THOSE ON THE DTO: id, clientEntityId, employeeId, type,
 * documentNumber, issueDate, expiryDate, fileUrl, fileName, contentType, fileSize,
 * isActive, isDeleted, deletedAt, createdAt, updatedAt, status, details. There is
 * no owner name on this DTO either, which is why ownership is a separate section,
 * and no createdBy/updatedBy, so no author is shown.
 *
 * ------------------------------------------------------------------
 * STATUS VOCABULARY IS THE DETAIL ENDPOINT'S, AND IT IS NOT THE REGISTRY'S.
 * ------------------------------------------------------------------
 * `status` here is the DocumentStatus ENUM (Active / ExpiringSoon / Overdue /
 * InRenewal), computed by a different SQL fragment from the registry's plain
 * string label. The same document therefore reads "Expired" in the registry table
 * and "Overdue" here.
 *
 * Both values are preserved exactly as their own endpoint returned them and
 * neither is translated into the other. That is not an oversight to be tidied away
 * later: the two labels come from two different server-side rules, and collapsing
 * them would mean the browser had decided the backend's two vocabularies mean the
 * same thing. The provenance note below the row makes that explicit to the reader
 * instead of hiding it.
 *
 * ------------------------------------------------------------------
 * THE FILE COMES FROM AN AUTHORIZED ENDPOINT, AND THE STORED PATH IS NOT SHOWN.
 * ------------------------------------------------------------------
 * This section used to open GET /admin/documents/{id}/scan, take the `fileUrl` off
 * that response and navigate a pre-opened tab straight to it, with a hand-rolled
 * popup-blocker workaround and a fallback anchor.
 *
 * That was built around a stored reference that is not a retrievable URL: it is a
 * relative key describing where the file sits inside the server's storage, so the
 * tab it opened was a 404 at best, and displaying the key told an admin something
 * about internal layout that has no business being on screen. The whole popup
 * dance existed only to work around that.
 *
 * The bytes now come from GET /admin/documents/{id}/file, which authorizes the
 * caller and streams the content, and DocumentFilePanel turns them into a
 * short-lived object URL. No stored path is rendered, and the popup workaround is
 * gone with the reason for it.
 *
 * The action is still gated on a real `fileUrl` on the DTO. That value is a
 * reliable "does this document have a file at all" signal even though it is no
 * longer displayed, and offering a button that is certain to 404 is worse than
 * offering none.
 */
function AdminDocumentOverviewSection({ document }) {
  const loadFile = useCallback(() => getAdminDocumentFile(document?.id), [document?.id])

  const hasFile = Boolean(presentText(document?.fileUrl))
  const detailsText = detailsJsonText(document?.details)

  return (
    <AdminClientSection
      title="Overview"
      description="The document record exactly as the backend stores it."
    >
      <div className="flex flex-col gap-6">
        <section>
          <h3 className="mb-1 text-sm font-semibold tracking-tight text-[#16181D]">
            Document
          </h3>
          <dl>
            <AdminDetailRow
              label="Type"
              value={documentTypeLabel(document?.type) ?? displayText(null)}
            />
            <AdminDetailRow
              label="Document number"
              value={displayText(document?.documentNumber)}
              mono
            />
            <AdminDetailRow
              label="File name"
              value={displayText(document?.fileName)}
            />
            <AdminDetailRow
              label="Content type"
              value={displayText(document?.contentType)}
            />
            <AdminDetailRow
              label="File size"
              value={formatFileSize(document?.fileSize)}
            />
          </dl>
        </section>

        <section>
          <h3 className="mb-1 text-sm font-semibold tracking-tight text-[#16181D]">
            Validity
          </h3>
          <dl>
            <AdminDetailRow
              label="Issue date"
              value={formatDate(document?.issueDate)}
            />
            <AdminDetailRow
              label="Expiry date"
              value={formatDate(document?.expiryDate)}
            />
            <AdminDetailRow
              label="Stored status"
              value={
                <StatusPill
                  label={storedStatusLabel(document?.status) ?? 'Unknown'}
                  tone={storedStatusTone(document?.status)}
                />
              }
            />
          </dl>

          {/*
            Provenance note. The registry table labels the same condition with a
            different word, and a reader comparing the two views would otherwise have
            no way to tell whether one of them was wrong.
          */}
          <p className="mt-2 rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA] px-3.5 py-2.5 text-xs text-[#6B7280]">
            This is the status stored on the document record, using the document
            vocabulary (<span className="font-semibold text-[#16181D]">Active
            </span>, <span className="font-semibold text-[#16181D]">ExpiringSoon
            </span>, <span className="font-semibold text-[#16181D]">Overdue</span>,{' '}
            <span className="font-semibold text-[#16181D]">InRenewal</span>). The
            expiry registry list labels the same condition{' '}
            <span className="font-semibold text-[#16181D]">Expired</span>. Both come
            from the backend and both are shown as returned; neither is converted
            into the other here.
          </p>
        </section>

        <section>
          <h3 className="mb-1 text-sm font-semibold tracking-tight text-[#16181D]">
            File
          </h3>
          {hasFile ? (
            <DocumentFilePanel
              load={loadFile}
              identity={['admin-document', document?.id]}
              fileName={document?.fileName}
              contentType={document?.contentType}
              label="View the file"
            />
          ) : (
            <p className="text-xs text-[#6B7280]">
              This document has no associated file, so there is nothing to open.
            </p>
          )}
        </section>

        <section>
          <h3 className="mb-1 text-sm font-semibold tracking-tight text-[#16181D]">
            Record
          </h3>
          <dl>
            <AdminDetailRow
              label="Active"
              value={document?.isActive ? 'Yes' : 'No'}
            />
            <AdminDetailRow
              label="Deleted"
              value={document?.isDeleted ? 'Yes' : 'No'}
            />
            <AdminDetailRow
              label="Created"
              value={formatDateTime(document?.createdAt)}
            />
            <AdminDetailRow
              label="Last updated"
              value={formatDateTime(document?.updatedAt)}
            />
            {document?.deletedAt && (
              <AdminDetailRow
                label="Deleted at"
                value={formatDateTime(document.deletedAt)}
              />
            )}
          </dl>
        </section>

        {detailsText && (
          <section>
            <h3 className="mb-1 text-sm font-semibold tracking-tight text-[#16181D]">
              Details
            </h3>
            {/*
              `details` is a free-form JSON object with no schema the Admin API
              documents, so it is rendered verbatim as JSON rather than being mapped
              onto invented field labels.
            */}
            <pre className="max-h-64 overflow-auto whitespace-pre-wrap break-words rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA] p-3.5 font-mono text-xs text-[#16181D]">
              {detailsText}
            </pre>
          </section>
        )}
      </div>
    </AdminClientSection>
  )
}

export default AdminDocumentOverviewSection
