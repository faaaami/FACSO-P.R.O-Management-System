import apiClient from '../axios'

/**
 * Admin document endpoints. `[Authorize(Roles = "Admin")]` under /api/v1/admin.
 *
 * KNOWN LIMITATION (drives the approved Documents IA): there is NO global
 * Admin document list/search endpoint. The only cross-entity document query is
 * GET /documents/expiring. Everything else is reached through a parent
 * (entity documents, employee documents) or a single document id.
 */

/**
 * GET /api/v1/admin/documents/expiring
 * Query: days (30), page (1), pageSize (20), includeExpired (false)
 * This is the cross-entity document registry the Admin Documents page uses.
 * Returns { items, page, pageSize, totalCount }
 *   item: ExpiringDocumentListItemDto { id, clientEntityId?, employeeId?, type,
 *     documentNumber, issueDate?, expiryDate?, fileName?, isActive, isDeleted,
 *     daysRemaining, status }
 */
export async function getAdminExpiringDocuments(params = {}) {
  const response = await apiClient.get('/admin/documents/expiring', { params })
  return response.data.data
}

/**
 * GET /api/v1/admin/entities/{entityId}/documents
 * Query: includeDeleted (false) — AND NOTHING ELSE.
 *
 * CORRECTION (Admin Documents Phase 2): this wrapper previously documented
 * `page` / `pageSize`, which was wrong. The controller action binds only
 * `includeDeleted`, and GetClientEntityDocumentsResponseDto is
 * { items, totalCount } with no page fields, so this route is UNPAGINATED. Passing
 * page/pageSize would be silently ignored, and no pager may be built from it.
 *
 *   item: ClientEntityDocumentListItemDto { id, clientEntityId, type,
 *     documentNumber, issueDate?, expiryDate?, fileUrl?, fileName?, contentType?,
 *     fileSize?, isActive, isDeleted, createdAt, updatedAt, status }
 *
 * Note the `status` here is the DocumentStatus ENUM (Active / ExpiringSoon /
 * Overdue / InRenewal), NOT the registry's plain-string vocabulary used by
 * /documents/expiring. The two must not be reconciled in the browser.
 */
export async function getAdminEntityDocuments(entityId, params = {}) {
  const response = await apiClient.get(`/admin/entities/${entityId}/documents`, {
    params,
  })
  return response.data.data
}

/**
 * GET /api/v1/admin/documents/{documentId}
 * Query: includeDeleted (false)
 */
export async function getAdminDocumentById(documentId, params = {}) {
  const response = await apiClient.get(`/admin/documents/${documentId}`, { params })
  return response.data.data
}

/**
 * GET /api/v1/admin/documents/{documentId}/versions
 * Query: includeDeleted (false)
 */
export async function getAdminDocumentVersions(documentId, params = {}) {
  const response = await apiClient.get(`/admin/documents/${documentId}/versions`, {
    params,
  })
  return response.data.data
}

/** GET /api/v1/admin/documents/{documentId}/dependencies */
export async function getAdminDocumentDependencies(documentId) {
  const response = await apiClient.get(`/admin/documents/${documentId}/dependencies`)
  return response.data.data
}

/**
 * GET /api/v1/admin/document-types/{documentType}/schema
 * Returns DocumentTypeSchemaResponseDto { documentType, fields }
 */
export async function getAdminDocumentTypeSchema(documentType) {
  const response = await apiClient.get(`/admin/document-types/${documentType}/schema`)
  return response.data.data
}

// ---------------------------------------------------------------------------
// D1: document registration — upload, review, confirm.
// ---------------------------------------------------------------------------

/**
 * The shared axios instance carries a 10s default timeout, which a synchronous
 * extraction cannot meet: a scanned PDF is rasterised and OCR'd inside the
 * request, bounded only by the server's own OCR deadline. The extract call
 * therefore opts into a longer client-side timeout. The timeout is a transport
 * guard, not an upload limit — the server still enforces 20 MB and the whole
 * file is streamed to it either way.
 */
const EXTRACTION_TIMEOUT_MS = 180_000

/**
 * Uploads a document and runs extraction synchronously.
 * POST /api/v1/admin/documents/extract
 * FormData: file
 * Returns ExtractDocumentResponseDto { extractionId, ...suggestions }
 *
 * `Content-Type` is deliberately NOT set: the browser must generate the
 * multipart boundary itself. The shared instance defaults to
 * application/json, which is dropped here by passing `undefined`.
 *
 * Server-side validation is authoritative: extension, content type, MIME /
 * extension agreement, magic bytes and the 1 B - 20 MB size range are all
 * re-checked on upload.
 */
export async function extractAdminDocument(file) {
  const form = new FormData()
  form.append('file', file)

  const response = await apiClient.post('/admin/documents/extract', form, {
    headers: { 'Content-Type': undefined },
    timeout: EXTRACTION_TIMEOUT_MS,
  })
  return response.data.data
}

/**
 * Lists extraction drafts awaiting review across the Admin document scope.
 * GET /api/v1/admin/documents/extractions
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
 * THIS IS A REVIEW QUEUE, NOT A DOCUMENT BROWSER. It lists extraction drafts
 * only; confirmed documents are reached through the expiry registry, the
 * client entity record or the employee record, as before. There is no
 * `fileUrl` here: the file endpoint streams the bytes.
 */
export async function getAdminPendingDocumentExtractions(params = {}) {
  const response = await apiClient.get('/admin/documents/extractions', { params })
  return response.data.data
}

/**
 * Loads one extraction draft's review payload.
 * GET /api/v1/admin/documents/extractions/{extractionId}
 * Returns GetDocumentExtractionResponseDto — `suggestedDetails` is a
 * non-authoritative suggestion only, and the internal storage reference is not
 * part of this contract.
 */
export async function getAdminDocumentExtraction(extractionId) {
  const response = await apiClient.get(
    `/admin/documents/extractions/${extractionId}`,
  )
  return response.data.data
}

/**
 * Streams an extraction draft's file for preview.
 * GET /api/v1/admin/documents/extractions/{extractionId}/file
 *
 * Returns the raw bytes (never a JSON envelope), so `responseType: 'blob'` is
 * required. The caller must revoke any object URL it creates.
 */
export async function getAdminDocumentExtractionFile(extractionId) {
  const response = await apiClient.get(
    `/admin/documents/extractions/${extractionId}/file`,
    { responseType: 'blob' },
  )
  return response.data
}

/**
 * Confirms an extraction draft, creating the operational Document.
 * POST /api/v1/admin/documents/extractions/{extractionId}/confirm
 * Body: { employeeId | clientEntityId, type, documentNumber, issueDate?,
 *         expiryDate?, details }
 *
 * The file fields (fileUrl / fileName / contentType / fileSize) are
 * deliberately NOT sent: the server's stored reference is authoritative and
 * confirmation copies it onto the Document.
 * Returns ConfirmDocumentResponseDto — `documentId` is the new document.
 */
export async function confirmAdminDocumentExtraction(extractionId, payload) {
  const response = await apiClient.post(
    `/admin/documents/extractions/${extractionId}/confirm`,
    payload,
  )
  return response.data.data
}

/**
 * Downloads a confirmed document's file through the authorized endpoint.
 * GET /api/v1/admin/documents/{documentId}/file
 *
 * The scan route returns a relative storage reference that is not
 * browser-retrievable, so the file must come from here. Returns raw bytes, so
 * `responseType: 'blob'` is required; the caller revokes the object URL.
 */
export async function getAdminDocumentFile(documentId) {
  const response = await apiClient.get(`/admin/documents/${documentId}/file`, {
    responseType: 'blob',
  })
  return response.data
}
