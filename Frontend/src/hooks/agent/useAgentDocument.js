import { useQuery } from '@tanstack/react-query'
import {
  getAgentDocumentById,
  getAgentDocumentVersions,
  getAgentDocumentDependencies,
  getAgentDocumentTypeSchema,
} from '../../api/agent/documents'

/**
 * Loads a single document in the Agent portfolio for the read-only detail view.
 * Backing query GET /api/v1/agent/documents/{documentId}
 * Returns { data, isLoading, isError, error, refresh }.
 *   data: GetDocumentByIdResponseDto — see api/agent/documents for the full
 *   field list, including the free-form `details` JSON.
 */
export function useAgentDocument(documentId) {
  const query = useQuery({
    queryKey: ['agent', 'document', documentId],
    queryFn: () => getAgentDocumentById(documentId),
    enabled: Boolean(documentId),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error,
    refresh: query.refetch,
  }
}

/**
 * Loads the version history of a document in the Agent portfolio.
 * Backing query GET /api/v1/agent/documents/{documentId}/versions
 * Returns { items, totalCount, isLoading, isError, refresh }.
 */
export function useAgentDocumentVersions(documentId) {
  const query = useQuery({
    queryKey: ['agent', 'document', documentId, 'versions'],
    queryFn: () => getAgentDocumentVersions(documentId),
    enabled: Boolean(documentId),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    items: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error,
    refresh: query.refetch,
  }
}

/**
 * Loads the forward document dependencies (documents this one depends on).
 * Backing query GET /api/v1/agent/documents/{documentId}/dependencies
 * Returns { items, totalCount, isLoading, isError, refresh }.
 */
export function useAgentDocumentDependencies(documentId) {
  const query = useQuery({
    queryKey: ['agent', 'document', documentId, 'dependencies'],
    queryFn: () => getAgentDocumentDependencies(documentId),
    enabled: Boolean(documentId),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    items: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error,
    refresh: query.refetch,
  }
}

/**
 * Loads the field schema for a document type, used to label the free-form
 * `details` object in the detail view.
 * Backing query GET /api/v1/agent/document-types/{documentType}/schema
 * Returns { fields, isLoading, isError, refresh }.
 */
export function useAgentDocumentTypeSchema(documentType) {
  const query = useQuery({
    queryKey: ['agent', 'document-type-schema', documentType],
    queryFn: () => getAgentDocumentTypeSchema(documentType),
    enabled: Boolean(documentType),
    staleTime: 5 * 60_000,
    retry: 1,
  })

  return {
    data: query.data,
    fields: query.data?.fields ?? [],
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error,
    refresh: query.refetch,
  }
}
