import { AlertCircle, AlertTriangle, ScanLine } from 'lucide-react'
import { SOURCE_LABELS } from './documentExtractionForm'

/**
 * What the extractor said about one field: where the suggested value came
 * from, plus any errors (the value is known to be wrong or unreadable) and
 * warnings (the value needs checking against the document). Rendered under
 * the field's control on the review form; renders nothing when the field
 * has no extraction result.
 *
 * A field read from the MRZ is never shown as "verified": check digits cannot
 * catch every look-alike, so passport numbers always carry a reviewer note.
 */
function ExtractedFieldHint({ result }) {
  if (!result) return null

  const errors = result.errors ?? []
  const warnings = result.warnings ?? []
  const label = SOURCE_LABELS[result.source] ?? SOURCE_LABELS.Manual

  return (
    <div className="mt-1.5 space-y-1">
      <p className="inline-flex items-center gap-1 rounded-[6px] bg-[#F1F3F6] px-1.5 py-0.5 text-[11px] font-medium text-[#4B5563]">
        <ScanLine size={11} strokeWidth={2} aria-hidden="true" />
        {label}
      </p>

      {errors.map((message) => (
        <p key={`e-${message}`} role="alert" className="flex items-start gap-1 text-[11px] leading-snug text-[#DC2626]">
          <AlertCircle size={11} strokeWidth={2} className="mt-0.5 shrink-0" aria-hidden="true" />
          <span>{message}</span>
        </p>
      ))}

      {warnings.map((message) => (
        <p key={`w-${message}`} className="flex items-start gap-1 text-[11px] leading-snug text-[#B45309]">
          <AlertTriangle size={11} strokeWidth={2} className="mt-0.5 shrink-0" aria-hidden="true" />
          <span>{message}</span>
        </p>
      ))}
    </div>
  )
}

export default ExtractedFieldHint
