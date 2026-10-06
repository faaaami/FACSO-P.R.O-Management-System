import { useState } from 'react'
import { AlertTriangle, FileDown } from 'lucide-react'
import {
  getAdminRetainerInvoicePdf,
  getAdminServiceFeeInvoicePdf,
} from '../../../api/admin/invoices'

/**
 * Admin invoice PDF download. Retainer and Service Fee only.
 *
 * The existing client-module DownloadPdfButton is NOT reused and NOT modified: it
 * takes its fetchers from the client billing API, its two kinds are the client's
 * own, and the completed Client module is protected. This is the Admin equivalent
 * over the Admin wrappers, and it deliberately does the same three things the
 * client one does — check the content type, read a blob error payload for its
 * message, and revoke the object URL — because those are the parts that are easy to
 * get wrong rather than the parts that are module-specific.
 *
 * ------------------------------------------------------------------
 * A PDF RESPONSE IS A FILE, NOT A DTO.
 * ------------------------------------------------------------------
 * Both endpoints `return File(bytes, "application/pdf", fileName)`, so there is no
 * ApiResponse envelope to unwrap. The wrapper returns the whole Axios response and
 * the body is `response.data`, already a Blob because the request was made with
 * `responseType: 'blob'`.
 *
 * The consequence that matters is the FAILURE path. Because the response type is
 * 'blob', an error response body is ALSO a Blob: the server's JSON problem details
 * arrive as bytes, so `error.response.data.message` is undefined and a plain
 * `.message` read would show the reader Axios's generic "Request failed with status
 * code 500" instead of what actually went wrong. The error is therefore resolved
 * from the blob's own text.
 *
 * THE CONTENT TYPE IS CHECKED BEFORE THE URL IS EVER CREATED. A 200 can still carry
 * an error body, and opening a blob of JSON in a new tab would look like a
 * successful download of a broken file. The blob is read, and only a real PDF
 * becomes a download.
 *
 * NO MUTATION SEMANTICS. This is a GET. It changes nothing, so it carries no
 * confirmation, and it sits in the drawer's Overview tab as a plain control rather
 * than in an action area.
 *
 * Government Fees and Payment Orders have no PDF route on the backend at all, so no
 * control for them exists anywhere in this module.
 */
const RETAINER_FAILURE = 'This invoice PDF could not be downloaded.'
const FEE_FAILURE = 'This Service Fee invoice PDF could not be downloaded.'

/**
 * Recovers a human message from a blob error payload. The API has two error shapes
 * — `{ error: { message } }` and RFC 7807 ProblemDetails — and either may be
 * delivered as a blob here, so both are read. `error` is also read as a plain string,
 * because the wrapper error shape serialises it that way on some paths and
 * `parsed.error.message` is undefined for a string, which would silently drop a
 * message the server did send. Anything unparseable falls back to the caller's own
 * message rather than surfacing raw JSON.
 */
function messageFromBlob(blob, fallback) {
  if (!blob) return fallback

  return blob
    .text()
    .then((text) => {
      try {
        const parsed = JSON.parse(text)
        const nested = typeof parsed?.error === 'string' ? parsed.error : parsed?.error?.message

        return nested ?? parsed?.message ?? parsed?.title ?? fallback
      } catch {
        return fallback
      }
    })
    .catch(() => fallback)
}

function AdminInvoiceDownloadPdfButton({ kind, id, invoiceNumber, variant = 'outline' }) {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState(null)

  const isRetainer = kind === 'retainer'
  const fallbackMessage = isRetainer ? RETAINER_FAILURE : FEE_FAILURE
  const subject = invoiceNumber ? `invoice ${invoiceNumber}` : 'this invoice'

  async function handleDownload() {
    setError(null)
    setIsLoading(true)

    try {
      const fetchPdf = isRetainer
        ? getAdminRetainerInvoicePdf
        : getAdminServiceFeeInvoicePdf

      const response = await fetchPdf(id)
      const blob = response.data
      const isPdf = Boolean(blob?.type) && blob.type.includes('pdf')

      // A 200 carrying a non-PDF body is still a failure, and it is reported as one
      // rather than being handed to the browser to open.
      if (!blob || !isPdf) {
        setError(await messageFromBlob(blob, fallbackMessage))
        return
      }

      const url = URL.createObjectURL(blob)
      // `noopener` matters here: the opened document is a blob on this origin, so
      // without it the new tab would get a handle to this window.
      window.open(url, '_blank', 'noopener,noreferrer')
      // Revoked on a delay rather than immediately, because revoking synchronously
      // after window.open can cancel the load in some browsers.
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000)
    } catch (err) {
      setError(
        await messageFromBlob(err?.response?.data, fallbackMessage),
      )
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <span className="inline-flex flex-col items-start gap-1">
      <button
        type="button"
        disabled={isLoading}
        onClick={handleDownload}
        aria-label={
          isLoading
            ? `Preparing the PDF for ${subject}`
            : `Download the PDF for ${subject}`
        }
        className={`inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] px-3.5 py-1.5 text-xs font-semibold transition duration-150 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed disabled:opacity-50 ${
          variant === 'outline'
            ? 'border border-[#E2E4E9] bg-white text-[#6B7280] hover:bg-gray-50 hover:text-[#16181D]'
            : 'bg-[#1C1F26] text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] hover:bg-[#101319]'
        }`}
      >
        <FileDown size={13} strokeWidth={2} aria-hidden="true" />
        {isLoading ? 'Preparing…' : 'Download PDF'}
      </button>

      {error && (
        <span
          role="alert"
          className="flex max-w-[20rem] items-start gap-1 text-[11px] font-medium text-[#DC2626]"
        >
          <AlertTriangle size={12} className="mt-0.5 shrink-0" aria-hidden="true" />
          <span className="break-words">{error}</span>
        </span>
      )}
    </span>
  )
}

export default AdminInvoiceDownloadPdfButton
