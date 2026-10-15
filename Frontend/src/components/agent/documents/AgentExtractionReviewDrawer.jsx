import { useCallback } from 'react'
import { getAgentDocumentExtractionFile } from '../../../api/agent/documents'
import { useAgentDocumentTypeSchema } from '../../../hooks/agent/useAgentDocument'
import {
  useAgentDocumentExtraction,
  useConfirmAgentDocumentExtraction,
} from '../../../hooks/agent/useAgentDocumentExtraction'
import DocumentExtractionReviewDrawer from '../../documents/DocumentExtractionReviewDrawer'
import DocumentFilePanel from '../../documents/DocumentFilePanel'
import AgentDocumentOwnerFields from './AgentDocumentOwnerFields'

/**
 * Agent binding for the shared document review drawer.
 *
 * The shared component owns the schema, the form and the payload; this supplies
 * the Agent's own data sources, so the Agent can only ever be offered drafts and
 * owners it is already scoped to.
 *
 * The draft's file is loaded through GET /agent/documents/extractions/{id}/file,
 * which authorizes the Agent before streaming a single byte. There is no URL in
 * the payload to render, and none is invented.
 */
function AgentExtractionReviewDrawer({ extractionId, onClose, onConfirmed }) {
  const { data, isLoading, isError, refresh } = useAgentDocumentExtraction(extractionId)
  const confirm = useConfirmAgentDocumentExtraction()

  const loadFile = useCallback(() => getAgentDocumentExtractionFile(extractionId), [extractionId])

  const draftState = isLoading ? 'loading' : isError || !data ? 'error' : 'ready'

  return (
    <DocumentExtractionReviewDrawer
      extractionId={extractionId}
      draft={data}
      draftState={draftState}
      refreshDraft={refresh}
      schemaHook={useAgentDocumentTypeSchema}
      ownerPicker={(props) => <AgentDocumentOwnerFields {...props} />}
      confirm={confirm}
      onConfirmed={onConfirmed}
      onClose={onClose}
      filePanel={
        <DocumentFilePanel
          load={loadFile}
          identity={['agent-extraction', extractionId]}
          fileName={data?.fileName}
          contentType={data?.contentType}
          label="Preview the uploaded file"
        />
      }
    />
  )
}

export default AgentExtractionReviewDrawer
