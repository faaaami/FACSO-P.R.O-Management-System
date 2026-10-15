import { useEffect, useState } from 'react'
import { AlertCircle, Download, ExternalLink, Eye, Loader2, RefreshCw } from 'lucide-react'
import { useDocumentFileBlob } from '../../hooks/useDocumentFileBlob'

/**
 * Authorized file access for a document or an extraction draft.
 *
 * SHARED BY AGENT, ADMIN AND CLIENT. There is no browser-fetchable URL for a
 * stored file: the DTOs' relative storage reference is not a public path, and
 * showing it would leak internal layout. So the bytes always come from a
 * role-scoped endpoint that authorizes the caller first, and this component
 * turns them into a short-lived object URL.
 *
 * The URL is created only on request and revoked by useDocumentFileBlob when it
 * is replaced or the panel unmounts — a blob URL is not garbage collected, so
 * leaving them alive would pin every file ever opened.
 *
 * PROPS
 *   load        () => Promise<Blob> — the portal's authorized file endpoint
 *   identity    [role, id] for the document or draft being shown. Required:
 *               it is what tells the panel "this is a different file", so bytes
 *               for one document can never be displayed under another. Keying
 *               off `fileName` instead would miss the case of two documents that
 *               share a name, or either having none at all.
 *   fileName    the name to save under; the server's Content-Disposition is not
 *               parsed, because the DTO already carries the real name
 *   contentType used only to decide between an inline image and a plain link
 *   label       button wording
 *   disabled
 */
function DocumentFilePanel({
  load,
  identity,
  fileName,
  contentType,
  label = 'View file',
  disabled = false,
}) {
  const [attempted, setAttempted] = useState(false)
  const identityKey = Array.isArray(identity) ? identity.join(':') : String(identity ?? '')

  const { url, isLoading, error, load: fetchFile, clear } = useDocumentFileBlob(load, {
    enabled: Boolean(load),
    identity: identityKey,
  })

  // A new file means the previous bytes are stale; drop the object URL rather
  // than leaving a viewer pointed at the last document.
  useEffect(() => {
    setAttempted(false)
    clear()
    // `clear` is stable; identityKey is the identity of "a different file".
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [identityKey])

  async function handleLoad() {
    setAttempted(true)
    await fetchFile()
  }

  const isImage = typeof contentType === 'string' && contentType.startsWith('image/')

  return (
    <div className="rounded-[10px] border border-[#E2E4E9] bg-white p-3.5">
      <div className="flex flex-wrap items-center gap-2">
        {!url ? (
          <button
            type="button"
            onClick={handleLoad}
            disabled={disabled || isLoading}
            className="inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3.5 py-2 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed disabled:opacity-50"
          >
            {isLoading ? (
              <>
                <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden="true" />
                <span>Loading file…</span>
              </>
            ) : (
              <>
                <Eye className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
                <span>{label}</span>
              </>
            )}
          </button>
        ) : (
          <>
            <a
              href={url}
              target="_blank"
              rel="noreferrer"
              className="inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3.5 py-2 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74]"
            >
              <ExternalLink className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
              <span>Open in new tab</span>
            </a>
            <a
              href={url}
              download={fileName || 'document'}
              className="inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3.5 py-2 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74]"
            >
              <Download className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
              <span>Download</span>
            </a>
            <button
              type="button"
              onClick={clear}
              className="inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] px-2.5 py-2 text-xs font-medium text-[#6B7280] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74]"
            >
              <RefreshCw className="h-3.5 w-3.5" strokeWidth={1.75} aria-hidden="true" />
              <span>Close preview</span>
            </button>
          </>
        )}
      </div>

      {error && attempted ? (
        <p
          role="alert"
          className="mt-2.5 flex items-start gap-1.5 text-xs leading-relaxed text-[#DC2626]"
        >
          <AlertCircle
            size={12}
            strokeWidth={2}
            className="mt-0.5 shrink-0"
            aria-hidden="true"
          />
          <span>{error}</span>
        </p>
      ) : null}

      {url && isImage ? (
        <img
          src={url}
          alt={fileName ? `Preview of ${fileName}` : 'Document preview'}
          className="mt-3 max-h-64 w-full rounded-[8px] border border-[#E2E4E9] object-contain"
        />
      ) : null}

      {url && !isImage ? (
        <p className="mt-2.5 text-xs leading-relaxed text-[#6B7280]">
          {contentType === 'application/pdf'
            ? 'PDF — open it in a new tab to read, or download a copy.'
            : 'Open it in a new tab to view, or download a copy.'}
        </p>
      ) : null}
    </div>
  )
}

export default DocumentFilePanel
