import { useCallback } from 'react'
import { Building2, FileText, Link2 } from 'lucide-react'
import Drawer from '../../client/documents/Drawer'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import StatusPill from '../../client/StatusPill'
import DocumentFilePanel from '../../documents/DocumentFilePanel'
import { getAgentDocumentFile } from '../../../api/agent/documents'
import { useAgentDocument } from '../../../hooks/agent/useAgentDocument'
import { useAgentEntityMaps } from '../../../hooks/agent/useAgentEntityMaps'
import {
  documentTypeLabel,
  documentStatusLabel,
  documentStatusTone,
  formatDate,
  formatFileSize,
} from './documentDisplay'
import AgentDocumentDetails from './AgentDocumentDetails'
import AgentDocumentVersions from './AgentDocumentVersions'
import AgentDocumentDependencies from './AgentDocumentDependencies'

function MetaBlock({ label, children }) {
  return (
    <div className="rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] p-3">
      <span className="block text-[11px] font-medium uppercase tracking-wider text-[#6B7280]">
        {label}
      </span>
      <div className="mt-1 break-words text-sm font-semibold text-[#16181D]">{children}</div>
    </div>
  )
}

function DetailRow({ label, value, mono }) {
  return (
    <div className="flex items-start justify-between gap-3 py-1.5">
      <span className="shrink-0 text-xs text-[#6B7280]">{label}</span>
      <span
        className={`min-w-0 break-words text-right text-xs font-medium text-[#16181D] ${mono ? 'font-mono' : ''}`}
      >
        {value ?? <span className="text-[#9CA3AF]">N/A</span>}
      </span>
    </div>
  )
}

function Section({ title, icon: Icon, children }) {
  return (
    <section>
      <h3 className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
        {Icon && <Icon size={13} strokeWidth={1.75} aria-hidden="true" />}
        {title}
      </h3>
      {children}
    </section>
  )
}

/**
 * Read-only document detail drawer for the Agent portal. Shows the document
 * overview, owner (resolved from the entity maps), the schema-labelled
 * `details` object, version history, and forward dependencies. No write
 * actions are exposed here.
 */
function AgentDocumentDetailDrawer({ documentId, onClose }) {
  const { data: document, isLoading, isError, refresh } = useAgentDocument(documentId)
  const { resolveOwner } = useAgentEntityMaps()

  // A new callback identity per document, so the file panel drops the previous
  // document's object URL instead of showing stale bytes.
  const loadFile = useCallback(() => getAgentDocumentFile(documentId), [documentId])

  let content

  if (isLoading) {
    content = <LoadingState label="Loading document details…" />
  } else if (isError) {
    content = (
      <ErrorState
        message="Could not load this document. It may have been removed or is no longer assigned to you."
        onRetry={() => refresh()}
      />
    )
  } else if (!document) {
    content = <p className="text-sm text-[#6B7280]">Document is not available.</p>
  } else {
    const owner = resolveOwner(document)
    content = (
      <div className="space-y-5">
        <div className="flex flex-col gap-3 rounded-[10px] bg-[#F7F8FA] border border-[#E2E4E9] p-4">
          <StatusPill
            label={documentStatusLabel(document.status)}
            tone={documentStatusTone(document.status)}
          />
          {document.deletedAt && (
            <p className="text-xs font-medium text-[#DC2626]">
              This document was deleted on {formatDate(document.deletedAt)}.
            </p>
          )}
        </div>

        <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3">
          <div className="flex items-center gap-2.5">
            <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#6B7280]">
              <Building2 size={15} strokeWidth={1.75} aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <p className="text-[11px] font-medium uppercase tracking-wider text-[#6B7280]">Owner</p>
              <p className="mt-0.5 truncate text-sm font-semibold text-[#16181D]">
                {owner ? (owner.name ?? owner.fallback) : 'Unassigned'}
              </p>
            </div>
          </div>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <MetaBlock label="Type">{documentTypeLabel(document.type)}</MetaBlock>
          <MetaBlock label="Document #">
            <span className="font-mono">{document.documentNumber || '—'}</span>
          </MetaBlock>
          <MetaBlock label="Issued">{formatDate(document.issueDate) ?? '—'}</MetaBlock>
          <MetaBlock label="Expires">{formatDate(document.expiryDate) ?? '—'}</MetaBlock>
        </div>

        <Section title="Details" icon={FileText}>
          <AgentDocumentDetails document={document} />
        </Section>

        <Section title="File" icon={FileText}>
          <div className="space-y-3">
            <div className="divide-y divide-[#E2E4E9]">
              <DetailRow label="File name" value={document.fileName} />
              <DetailRow label="Content type" value={document.contentType} />
              <DetailRow
                label="Size"
                value={formatFileSize(document.fileSize) ?? 'N/A'}
              />
            </div>
            {/*
              The stored `fileUrl` is deliberately NOT rendered. It is a relative
              storage reference — an `extractions/...` or `documents/...` key that
              describes where the file lives inside the server, not a URL a browser
              can fetch, so showing it was both useless and a disclosure of internal
              layout. The bytes come from GET /agent/documents/{id}/file, which
              authorizes this Agent against the document's task scope first.
            */}
            <DocumentFilePanel
              load={loadFile}
              identity={['agent-document', document.id]}
              fileName={document.fileName}
              contentType={document.contentType}
              label="View the document file"
            />
          </div>
        </Section>

        <Section title="Versions" icon={FileText}>
          <AgentDocumentVersions documentId={documentId} />
        </Section>

        <Section title="Dependencies" icon={Link2}>
          <AgentDocumentDependencies documentId={documentId} />
        </Section>
      </div>
    )
  }

  return (
    <Drawer
      title={document ? documentTypeLabel(document.type) : 'Document'}
      icon={FileText}
      subtitle={document?.documentNumber ? `Document #${document.documentNumber}` : 'Document details'}
      onClose={onClose}
    >
      {content}
    </Drawer>
  )
}

export default AgentDocumentDetailDrawer
