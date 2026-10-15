import { useQuery } from '@tanstack/react-query'
import {
  getAdminDocumentById,
  getAdminDocumentDependencies,
  getAdminDocumentTypeSchema,
  getAdminDocumentVersions,
  getAdminEntityDocuments,
  getAdminExpiringDocuments,
} from '../../api/admin/documents'

/**
 * Expiry & Renewal Registry — the Admin Documents data source.
 *
 * APPROVED DECISION Q1: there is no global Admin document list/search endpoint,
 * so the registry is built on GET /admin/documents/expiring, the only
 * cross-entity document query the backend offers. Documents that are not near
 * expiry stay reachable through their client/entity or employee record.
 *
 * Backing query GET /api/v1/admin/documents/expiring
 * Params: { days = 30, page = 1, pageSize = 20, includeExpired = false }
 */
export function useAdminExpiringDocuments(params = {}) {
  const days = params?.days ?? 30
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20
  const includeExpired = params?.includeExpired ?? false

  const query = useQuery({
    queryKey: ['admin', 'documents', 'expiring', { days, page, pageSize, includeExpired }],
    queryFn: () => getAdminExpiringDocuments({ days, page, pageSize, includeExpired }),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    items: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
    page: query.data?.page ?? page,
    pageSize: query.data?.pageSize ?? pageSize,
    loading: query.isLoading,
    isLoading: query.isLoading,
    // Distinguishes a first load from a background refetch so a list can keep
    // showing rows during the latter. Purely additive.
    isFetching: query.isFetching,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}

/**
 * Single document. Only reachable once an id is known (from the registry, an
 * entity, or an employee) — there is no way to look a document up by name.
 * Backing query GET /api/v1/admin/documents/{documentId}
 *
 * Returns null with an explicit 404 DOCUMENT_NOT_FOUND when the id is unknown, so a
 * missing document is an error with a retry rather than an empty record.
 */
export function useAdminDocument(documentId, params = {}) {
  const includeDeleted = params?.includeDeleted ?? false

  const query = useQuery({
    queryKey: ['admin', 'document', documentId, { includeDeleted }],
    queryFn: () => getAdminDocumentById(documentId, { includeDeleted }),
    enabled: Boolean(documentId),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    loading: query.isLoading,
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}

/**
 * Version history for one document.
 * Backing query GET /api/v1/admin/documents/{documentId}/versions
 * Query: includeDeleted (false)
 * Returns { items, totalCount } — UNPAGINATED, so no pager may be built from it and
 * totalCount is a plain row count.
 *
 *   item: DocumentVersionListItemDto { id, documentId, versionNumber, fileUrl,
 *     fileName?, contentType?, fileSize?, isDeleted, createdAt, updatedAt }
 *
 * `versionNumber` is supplied by the backend, so it is displayed. There is NO
 * uploader/author field and no "current version" flag on the DTO, so neither is
 * inferred here — a document's own row is the current one, and saying so from a
 * list ordering would be a guess.
 */
export function useAdminDocumentVersions(documentId, params = {}) {
  const includeDeleted = params?.includeDeleted ?? false

  const query = useQuery({
    queryKey: ['admin', 'document-versions', documentId, { includeDeleted }],
    queryFn: () => getAdminDocumentVersions(documentId, { includeDeleted }),
    enabled: Boolean(documentId),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    items: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
    loading: query.isLoading,
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}

/**
 * Documents this document depends on — read-only.
 * Backing query GET /api/v1/admin/documents/{documentId}/dependencies
 * The action binds no query parameters at all.
 * Returns { documentId, items, totalCount } — UNPAGINATED, so no pager may be
 * built from it and totalCount is a plain row count.
 *
 *   item: DocumentDependencyItemDto { id, dependsOnDocumentId, documentNumber,
 *     expiryDate, isActive, isDeleted }
 *
 * `totalCount` is a real count on this DTO, unlike the contract list which
 * returns no total at all, so it is safe to display.
 *
 * A row names only the DEPENDED-ON document. There is no document title, owner,
 * or file name on the item, so none is shown and none is guessed. `isActive` and
 * `isDeleted` describe the dependency edge, so they are rendered as recorded.
 *
 * Order and shape are left exactly as returned. A dependency graph can contain a
 * cycle and no convention here defines one, so this hook does not sort,
 * deduplicate, detect cycles, or claim the list is topologically ordered.
 */
export function useAdminDocumentDependencies(documentId) {
  const query = useQuery({
    queryKey: ['admin', 'document-dependencies', documentId],
    queryFn: () => getAdminDocumentDependencies(documentId),
    enabled: Boolean(documentId),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    items: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
    loading: query.isLoading,
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}

/**
 * The field schema for one document type.
 * Backing query GET /api/v1/admin/document-types/{documentType}/schema
 * Returns { documentType, fields } where each field is
 *   { key, label, valueKind, required, maxLength }
 * and valueKind is the string enum 'String' | 'Date' | 'Number' | 'Boolean'.
 *
 * D1 uses this to drive the review drawer's dynamic detail form. The schema is
 * the single source of truth for which fields a type has, so the form is never
 * built from a hard-coded list — and `isError` is load-bearing there: an
 * unavailable schema must block confirmation rather than be treated as "this
 * type has no extra fields".
 *
 * Mirrors useAgentDocumentTypeSchema so the shared review drawer can take
 * either hook as a prop.
 */
export function useAdminDocumentTypeSchema(documentType) {
  const query = useQuery({
    queryKey: ['admin', 'document-type-schema', documentType],
    queryFn: () => getAdminDocumentTypeSchema(documentType),
    enabled: Boolean(documentType),
    staleTime: 5 * 60_000,
    retry: 1,
  })

  return {
    fields: query.data?.fields ?? [],
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error,
    refresh: query.refetch,
  }
}

/**
 * Documents held against one legal entity.
 * Backing query GET /api/v1/admin/entities/{entityId}/documents
 * Params: { includeDeleted = false } — the only parameter the action binds.
 * Returns { items, totalCount } — UNPAGINATED, so no pager may be built from it
 * and totalCount is a plain row count.
 *
 *   item: ClientEntityDocumentListItemDto { id, clientEntityId, type,
 *     documentNumber, issueDate?, expiryDate?, fileUrl?, fileName?, contentType?,
 *     fileSize?, isActive, isDeleted, createdAt, updatedAt, status }
 *
 * This route is scoped by the entity id alone, unlike the client-scoped entity
 * detail route which takes both ids. An entity id is a GUID unique across the
 * table, so a valid id is a valid lookup; a wrong one yields the endpoint's own
 * 404 rather than another company's document.
 *
 * `status` here is the DocumentStatus ENUM (Active / ExpiringSoon / Overdue /
 * InRenewal) as the backend computed it, NOT the plain-string vocabulary the
 * /documents/expiring registry returns. Callers must use the stored-status label
 * and tone helpers and must not reconcile the two vocabularies in the browser.
 */
export function useAdminEntityDocuments(entityId, params = {}) {
  const includeDeleted = params?.includeDeleted ?? false

  const query = useQuery({
    queryKey: ['admin', 'entity-documents', entityId, { includeDeleted }],
    queryFn: () => getAdminEntityDocuments(entityId, { includeDeleted }),
    enabled: Boolean(entityId),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    items: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
    loading: query.isLoading,
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}
