import { useState } from 'react'
import { AlertTriangle, FileDown } from 'lucide-react'
import { getRetainerInvoicePdf, getServiceFeeInvoicePdf } from '../../../api/client/billing'

function messageFromBlob(blob) {
  return blob
    .text()
    .then((text) => {
      try {
        const parsed = JSON.parse(text)
        return parsed?.message || parsed?.error || 'The invoice PDF could not be downloaded.'
      } catch {
        return 'The invoice PDF could not be downloaded.'
      }
    })
    .catch(() => 'The invoice PDF could not be downloaded.')
}

function DownloadPdfButton({ kind, id, label = 'Get PDF', variant = 'outline' }) {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState(null)

  const fetchFn = kind === 'retainer' ? getRetainerInvoicePdf : getServiceFeeInvoicePdf

  const handleDownload = async () => {
    setError(null)
    setIsLoading(true)
    try {
      const response = await fetchFn(id)
      const blob = response.data
      const isPdf = blob?.type === 'application/pdf' || blob?.type?.includes('pdf')

      if (!blob || !isPdf) {
        setError(await messageFromBlob(blob))
        return
      }

      const url = URL.createObjectURL(blob)
      window.open(url, '_blank', 'noopener,noreferrer')
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000)
    } catch (err) {
      setError(
        err?.response?.status === 404
          ? 'This invoice PDF is not available.'
          : 'The invoice PDF could not be downloaded.',
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
        className={`inline-flex items-center gap-1.5 rounded-[10px] px-3.5 py-1.5 text-xs font-semibold transition duration-150 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer ${
          variant === 'outline'
            ? 'border border-[#E2E4E9] bg-white text-[#6B7280] hover:bg-gray-50 hover:text-[#16181D]'
            : 'bg-[#1C1F26] text-white hover:bg-[#101319] shadow-[0_1px_3px_rgba(28,31,38,0.06)]'
        }`}
      >
        <FileDown size={13} strokeWidth={2} aria-hidden="true" />
        {isLoading ? 'Preparing…' : label}
      </button>
      {error && (
        <span className="flex max-w-[16rem] items-start gap-1 text-[11px] font-medium text-[#DC2626]">
          <AlertTriangle size={12} className="shrink-0 mt-0.5" aria-hidden="true" />
          <span className="break-words">{error}</span>
        </span>
      )}
    </span>
  )
}

export default DownloadPdfButton