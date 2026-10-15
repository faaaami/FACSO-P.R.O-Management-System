import { useRef, useState } from 'react'
import { AlertCircle, Loader2, UploadCloud } from 'lucide-react'
import { toast } from 'sonner'
import { extractApiErrorMessage } from '../../utils/apiError'

/**
 * These restate the server's own upload rules so a wrong file is refused before
 * a 20 MB round trip. The server re-checks all of them — including the magic
 * bytes, which a browser cannot — and its message is the one that is shown when
 * it disagrees, so this is a courtesy, not the enforcement point.
 */
const MAX_FILE_SIZE_BYTES = 20 * 1024 * 1024
const MAX_FILE_NAME_LENGTH = 255
const ACCEPTED_EXTENSIONS = ['pdf', 'jpg', 'jpeg', 'png']
const ACCEPTED_MIME_TYPES = ['application/pdf', 'image/jpeg', 'image/png']

/**
 * Document upload + extraction. SHARED BY AGENT AND ADMIN.
 *
 * EXTRACTION IS SYNCHRONOUS, AND THE UI SAYS SO. The whole pipeline — store,
 * rasterise, OCR, parse — happens inside the upload request, so the button
 * stays in an "Extracting…" state for as long as it takes rather than returning
 * instantly. The request also opts out of the shared client's 10s default
 * timeout for the same reason; that timeout is a transport guard, not a size
 * limit.
 *
 * The upload is sent as soon as a file is chosen rather than behind a separate
 * submit, because there is no draft to create first: the request both stores the
 * file and returns the extraction. A failure leaves nothing behind — the server
 * deletes a stored file whose extraction failed — so retrying is always safe.
 *
 * NO OWNER IS CHOSEN HERE. The extractor reads a file and cannot know who it
 * belongs to, so the owner is picked in the review drawer from a real, scoped
 * list.
 *
 * PROPS
 *   useExtract   the portal's upload mutation hook
 *   onExtracted  (extractionId) => void — opens the review drawer
 *   isAgent      copy only
 */
function DocumentUploadCard({ useExtract, onExtracted, isAgent = true }) {
  const inputRef = useRef(null)
  const [localError, setLocalError] = useState(null)
  const [selectedName, setSelectedName] = useState(null)

  const extract = useExtract()
  const isExtracting = extract.isPending

  function validate(file) {
    if (!file) return 'No file was chosen.'

    const name = file.name ?? ''
    if (name.length > MAX_FILE_NAME_LENGTH) {
      return `File name must be at most ${MAX_FILE_NAME_LENGTH} characters.`
    }

    const extension = name.includes('.') ? name.split('.').pop().toLowerCase() : ''
    if (!ACCEPTED_EXTENSIONS.includes(extension)) {
      return 'That file type is not accepted. Upload a PDF, JPG, JPEG or PNG.'
    }

    // A renamed file reports the wrong type here and is caught properly by the
    // server's magic-byte check; this only catches the honest mismatch early.
    if (file.type && !ACCEPTED_MIME_TYPES.includes(file.type)) {
      return 'That file type is not accepted. Upload a PDF, JPG, JPEG or PNG.'
    }

    if (file.size < 1 || file.size > MAX_FILE_SIZE_BYTES) {
      return 'File size must be between 1 byte and 20 MB.'
    }

    return null
  }

  function handleChange(event) {
    const file = event.target.files?.[0]

    // Reset immediately so choosing the same file again after a failure still
    // fires a change event.
    event.target.value = ''

    if (!file) return

    const problem = validate(file)
    if (problem) {
      setLocalError(problem)
      setSelectedName(null)
      return
    }

    setLocalError(null)
    setSelectedName(file.name)

    extract.mutate(file, {
      onSuccess: (data) => {
        setSelectedName(null)
        toast.success('Extraction finished. Check the suggested values before confirming.')
        onExtracted?.(data?.extractionId)
      },
      onError: () => {
        // The chosen name is kept so the reader knows exactly which file failed.
      },
    })
  }

  const error = localError ?? (extract.isError ? extract.error : null)

  return (
    <div className="mb-4 rounded-[12px] border border-dashed border-[#D7DAE0] bg-[#F8F9FB] p-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-start gap-2.5">
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] border border-[#E2E4E9] bg-white text-[#16181D]">
            <UploadCloud size={16} strokeWidth={1.75} aria-hidden="true" />
          </span>
          <div className="min-w-0">
            <p className="text-sm font-semibold text-[#16181D]">
              {isAgent ? 'Upload a document to register' : 'Upload a document for review'}
            </p>
            <p className="mt-0.5 text-xs leading-relaxed text-[#6B7280]">
              PDF, JPG, JPEG or PNG, up to 20 MB. Extraction runs while the upload is in progress, and
              you confirm or correct everything it finds before it becomes a document.
            </p>
          </div>
        </div>

        <div className="flex shrink-0 items-center gap-2">
          <button
            type="button"
            onClick={() => inputRef.current?.click()}
            disabled={isExtracting}
            className="inline-flex cursor-pointer items-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {isExtracting ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                <span>Extracting…</span>
              </>
            ) : (
              <>
                <UploadCloud className="h-4 w-4" strokeWidth={1.75} aria-hidden="true" />
                <span>Choose file</span>
              </>
            )}
          </button>

          {/*
            Visually hidden but still focusable and reachable by assistive tech, so
            the control is not a mouse-only affordance. The visible button is the
            styled proxy that opens it.
          */}
          <input
            ref={inputRef}
            type="file"
            accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png"
            onChange={handleChange}
            disabled={isExtracting}
            className="sr-only"
            aria-label="Choose a document to upload and extract"
          />
        </div>
      </div>

      {isExtracting ? (
        <p aria-live="polite" className="mt-3 text-xs leading-relaxed text-[#6B7280]">
          Extracting {selectedName ? `${selectedName}` : 'the file'}. A scanned document takes longer
          — the page will keep working, but this stays busy until extraction finishes.
        </p>
      ) : null}

      {error ? (
        <p
          role="alert"
          className="mt-3 flex items-start gap-1.5 text-xs leading-relaxed text-[#DC2626]"
        >
          <AlertCircle size={12} strokeWidth={2} className="mt-0.5 shrink-0" aria-hidden="true" />
          <span>
            {extractApiErrorMessage(error, 'The document could not be uploaded.')}
            {selectedName && !localError ? ` (${selectedName})` : ''}
          </span>
        </p>
      ) : null}
    </div>
  )
}

export default DocumentUploadCard
