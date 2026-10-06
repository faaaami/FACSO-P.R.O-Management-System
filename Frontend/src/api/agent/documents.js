import apiClient from '../axios'

/**
 * Retrieves the paginated list of documents visible to the current Agent.
 * GET /api/v1/agent/documents
 * Query: { type?, status?, expiresBefore?, page = 1, pageSize = 20 }
 * Returns GetAgentDocumentsResponseDto { items, page, pageSize, totalCount }
 *   item: AgentDocumentListItemDto { id, clientEntityId?, employeeId?, type,
 *     documentNumber, issueDate?, expiryDate?, fileUrl?, fileName?,
 *     contentType?, fileSize?, isActive, isDeleted, createdAt, updatedAt,
 *     status }
 */
export async function getAgentDocuments(params = {}) {
  const response = await apiClient.get('/agent/documents', { params })
  return response.data.data
}

/**
 * Retrieves documents expiring within the given window, scoped to the Agent's
 * task-linked document portfolio (a document is in scope only when it is the
 * document of a non-deleted renewal task assigned to the signed-in Agent).
 * GET /api/v1/agent/documents/expiring
 * Query: { days (1..365, default 30; the Agent UI offers 30/60/90),
 *          page = 1, pageSize = 20, includeExpired = false }
 * Returns GetExpiringDocumentsResponseDto { items, page, pageSize, totalCount }
 *   item: ExpiringDocumentListItemDto { id, clientEntityId?, employeeId?, type,
 *     documentNumber, issueDate?, expiryDate?, fileName?, isActive, isDeleted,
 *     daysRemaining, status }
 *   status is 'ExpiringSoon' | 'Active' | 'Expired'. daysRemaining is the
 *   backend's own signed, floored arithmetic (negative means days past
 *   expiry), so read expiry state from status, not from the sign of
 *   daysRemaining.
 */
export async function getAgentExpiringDocuments(params = {}) {
  const response = await apiClient.get('/agent/documents/expiring', { params })
  return response.data.data
}

/**
 * Retrieves a single document scoped to the Agent's task-linked portfolio.
 * GET /api/v1/agent/documents/{documentId}
 * Returns GetDocumentByIdResponseDto { id, clientEntityId?, employeeId?, type,
 *   documentNumber, issueDate?, expiryDate?, fileUrl?, fileName?, contentType?,
 *   fileSize?, isActive, isDeleted, deletedAt?, createdAt, updatedAt, status,
 *   details }
 *   `details` is a free-form JSON object (type-specific); render it through the
 *   document-type schema (see getAgentDocumentTypeSchema) rather than dumping
 *   raw keys. The endpoint does not expose an owner *name* — only the owner
 *   ids — so resolve names via useAgentEntityMaps and fall back to a short id.
 * There is intentionally no separate scan call: the detail DTO already carries
 * fileUrl/fileName/contentType, so a scan request would be a redundant
 * round-trip (and the Agent scan endpoint returns metadata only, no bytes).
 */
export async function getAgentDocumentById(documentId) {
  const response = await apiClient.get(`/agent/documents/${documentId}`)
  return response.data.data
}

/**
 * Retrieves the version history of a document in the Agent portfolio.
 * GET /api/v1/agent/documents/{documentId}/versions
 * Returns GetDocumentVersionsResponseDto { items, totalCount }
 *   item: DocumentVersionListItemDto { id, documentId, versionNumber, fileUrl,
 *     fileName?, contentType?, fileSize?, isDeleted, createdAt, updatedAt }
 */
export async function getAgentDocumentVersions(documentId) {
  const response = await apiClient.get(`/agent/documents/${documentId}/versions`)
  return response.data.data
}

/**
 * Retrieves the documents this document depends on (e.g. a visa depending on a
 * passport) in the Agent portfolio. Forward dependencies only — the reverse
 * direction ("what depends on this") is not exposed by the backend.
 * GET /api/v1/agent/documents/{documentId}/dependencies
 * Returns GetDocumentDependenciesResponseDto { documentId, items, totalCount }
 *   item: DocumentDependencyItemDto { id, dependsOnDocumentId, documentType,
 *     documentNumber, expiryDate?, isActive, isDeleted }
 */
export async function getAgentDocumentDependencies(documentId) {
  const response = await apiClient.get(`/agent/documents/${documentId}/dependencies`)
  return response.data.data
}

/**
 * Retrieves the field schema for a document type, used to label the free-form
 * `details` JSON in a readable way.
 * GET /api/v1/agent/document-types/{documentType}/schema
 *   documentType is a DocumentType enum name (e.g. 'Passport'); the backend
 *   parses it case-insensitively and 400s on an unknown value.
 * Returns DocumentTypeSchemaResponseDto { documentType, fields }
 *   field: DocumentFieldDefinitionResponseDto { key, label, valueKind, required,
 *     maxLength? }
 *   valueKind is 'String' | 'Date' | 'Number' | 'Boolean'.
 */
export async function getAgentDocumentTypeSchema(documentType) {
  const response = await apiClient.get(`/agent/document-types/${documentType}/schema`)
  return response.data.data
}

// ---------------------------------------------------------------------------
// D1: document registration — upload, review, confirm.
// ---------------------------------------------------------------------------

/**
 * The shared axios instance carries a 10s default timeout, which a synchronous
 * extraction cannot meet: a scanned PDF is rasterised and OCR'd inside the
 * request, bounded only by the server's own OCR deadline. The extract call
 * therefore opts into a longer client-side timeout. The timeout is a
 * transport guard, not an upload limit — the server still enforces 20 MB and
 * the whole file is streamed to it either way.
 */
const EXTRACTION_TIMEOUT_MS = 180_000

/**
 * Uploads a document and runs extraction synchronously.
 * POST /api/v1/agent/documents/extract
 * FormData: file
 * Returns ExtractDocumentResponseDto { extractionId, ...suggestions }
 *
 * `Content-Type` is deliberately NOT set: the browser must generate the
 * multipart boundary itself. The shared instance defaults to
 * application/json, which is dropped here by passing `undefined`, so axios
 * falls back to the browser-generated multipart header.
 *
 * Server-side validation is authoritative: extension, content type, MIME /
 * extension agreement, magic bytes and the 1 B - 20 MB size range are all
 * re-checked on upload.
 */
export async function extractAgentDocument(file) {
  const form = new FormData()
  form.append('file', file)

  const response = await apiClient.post('/agent/documents/extract', form, {
    headers: { 'Content-Type': undefined },
    timeout: EXTRACTION_TIMEOUT_MS,
  })
  return response.data.data
}

/**
 * Lists this agent's extraction drafts awaiting review.
 * GET /api/v1/agent/documents/extractions
 * Query: { page = 1, pageSize = 20, status? }
 *   status defaults to 'Pending' (the review queue) and also accepts
 *   'Confirmed' | 'Expired'. An unknown value is rejected with 400.
 * Returns GetPendingDocumentExtractionsResponseDto { items, page, pageSize,
 *   totalCount }
 *   item: PendingDocumentExtractionListItemDto { id, fileName, contentType,
 *     fileSize, suggestedType?, suggestedDocumentNumber?, suggestedIssueDate?,
 *     suggestedExpiryDate?, suggestedOwnerType?, confidence, status,
 *     expiresAt?, createdAt, createdBy? }
 *
 * `createdBy` is null for legacy rows that predate the ownership column. Those
 * are still confirmable by any in-scope agent, which is why they stay listed.
 * There is no `fileUrl` here: the file endpoint streams the bytes.
 */
export async function getAgentPendingDocumentExtractions(params = {}) {
  const response = await apiClient.get('/agent/documents/extractions', { params })
  return response.data.data
}

/**
 * Loads one extraction draft's review payload.
 * GET /api/v1/agent/documents/extractions/{extractionId}
 * Returns GetDocumentExtractionResponseDto { extractionId, fileName,
 *   contentType, fileSize, suggestedType?, suggestedDocumentNumber?,
 *   suggestedIssueDate?, suggestedExpiryDate?, suggestedOwnerType?,
 *   suggestedDetails, confidence, status, expiresAt? }
 *
 * `suggestedDetails` is a non-authoritative suggestion only. The internal
 * storage reference is not part of this contract.
 */
export async function getAgentDocumentExtraction(extractionId) {
  const response = await apiClient.get(
    `/agent/documents/extractions/${extractionId}`,
  )
  return response.data.data
}

/**
 * Streams an extraction draft's file for preview.
 * GET /api/v1/agent/documents/extractions/{extractionId}/file
 *
 * Returns the raw bytes (never a JSON envelope), so `responseType: 'blob'` is
 * required. The caller must revoke any object URL it creates.
 */
export async function getAgentDocumentExtractionFile(extractionId) {
  const response = await apiClient.get(
    `/agent/documents/extractions/${extractionId}/file`,
    { responseType: 'blob' },
  )
  return response.data
}

/**
 * Confirms an extraction draft, creating the operational Document.
 * POST /api/v1/agent/documents/extractions/{extractionId}/confirm
 * Body: { employeeId | clientEntityId, type, documentNumber, issueDate?,
 *         expiryDate?, details }
 *
 * The file fields (fileUrl / fileName / contentType / fileSize) are
 * deliberately NOT sent: the server's stored reference is authoritative and
 * confirmation copies it onto the Document.
 * Returns ConfirmDocumentResponseDto — `documentId` is the new document.
 */
export async function confirmAgentDocumentExtraction(extractionId, payload) {
  const response = await apiClient.post(
    `/agent/documents/extractions/${extractionId}/confirm`,
    payload,
  )
  return response.data.data
}

/**
 * Downloads a confirmed document's file through the authorized endpoint.
 * GET /api/v1/agent/documents/{documentId}/file
 *
 * The document list DTO's `fileUrl` is a relative storage reference that is
 * not browser-retrievable, so the file must come from here. Returns raw bytes,
 * so `responseType: 'blob'` is required; the caller revokes the object URL.
 */
export async function getAgentDocumentFile(documentId) {
  const response = await apiClient.get(`/agent/documents/${documentId}/file`, {
    responseType: 'blob',
  })
  return response.data
}
