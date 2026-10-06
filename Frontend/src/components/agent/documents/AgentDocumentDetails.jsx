import { useAgentDocumentTypeSchema } from '../../../hooks/agent/useAgentDocument'
import { formatDetailsValue } from './documentDisplay'

function DetailRow({ label, value, hint }) {
  return (
    <div className="flex items-start justify-between gap-3 py-2">
      <span className="min-w-0 text-xs text-[#6B7280]">
        {label}
        {hint && <span className="ml-1 text-[#9CA3AF]">({hint})</span>}
      </span>
      <span className="min-w-0 break-words text-right text-xs font-medium text-[#16181D]">
        {value}
      </span>
    </div>
  )
}

/**
 * Renders a document's free-form `details` JSON using the document-type schema
 * so field keys are shown as human labels. Any key present in `details` but
 * absent from the schema is still rendered (labelled by its raw key) so no
 * recorded data is hidden.
 */
function AgentDocumentDetails({ document }) {
  const { fields, isLoading } = useAgentDocumentTypeSchema(document?.type)
  const details = document?.details && typeof document.details === 'object' ? document.details : null
  const detailsKeys = details ? Object.keys(details) : []

  if (isLoading) {
    return <p className="text-xs text-[#9CA3AF]">Loading field labels…</p>
  }

  if (!details || detailsKeys.length === 0) {
    return <p className="text-xs text-[#9CA3AF]">No additional details recorded.</p>
  }

  const schemaKeys = new Set(fields.map((field) => field.key))
  const knownFields = fields.filter((field) => detailsKeys.includes(field.key))
  const extraKeys = detailsKeys.filter((key) => !schemaKeys.has(key))

  return (
    <div className="divide-y divide-[#E2E4E9]">
      {knownFields.map((field) => (
        <DetailRow
          key={field.key}
          label={field.label}
          hint={field.required ? 'required' : undefined}
          value={formatDetailsValue(details[field.key], field.valueKind)}
        />
      ))}
      {extraKeys.map((key) => (
        <DetailRow
          key={key}
          label={key}
          value={formatDetailsValue(details[key], undefined)}
        />
      ))}
    </div>
  )
}

export default AgentDocumentDetails
