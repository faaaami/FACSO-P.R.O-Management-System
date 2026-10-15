import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  confirmAgentDocumentExtraction,
  extractAgentDocument,
  getAgentDocumentExtraction,
  getAgentPendingDocumentExtractions,
} from '../../api/agent/documents'

/**
 * D1 — Agent document registration: upload, review queue, confirm.
 *
 * QUERY KEYS (matching the verified existing Agent keys):
 *   ['agent','document-extraction', id]      one draft
 *   ['agent','document-extractions', {...}]  the paged review queue
 *
 * These are deliberately new prefixes rather than a reuse of
 * ['agent','document',id]: a draft is not a document and must not be
 * invalidated by, or invalidate, the confirmed-document caches.
 */

/**
 * Loads one extraction draft's review payload.
 * Backing query GET /api/v1/agent/documents/extractions/{id}
 * Returns { data, isLoading, isError, error, refresh }.
 */
export function useAgentDocumentExtraction(extractionId) {
  const query = useQuery({
    queryKey: ['agent', 'document-extraction', extractionId],
    queryFn: () => getAgentDocumentExtraction(extractionId),
    enabled: Boolean(extractionId),
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
 * Loads the agent's paged queue of extraction drafts awaiting review.
 * Backing query GET /api/v1/agent/documents/extractions
 * Params: { page = 1, pageSize = 20, status? }
 * Returns { data, items, totalCount, page, pageSize, isLoading, isError,
 *   error, isFetching, refresh }.
 */
export function useAgentPendingDocumentExtractions(params = {}) {
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20
  const status = params?.status

  const query = useQuery({
    queryKey: ['agent', 'document-extractions', { page, pageSize, status }],
    queryFn: () => getAgentPendingDocumentExtractions({ page, pageSize, status }),
    staleTime: 15_000,
    retry: 1,
  })

  return {
    data: query.data,
    items: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
    page: query.data?.page ?? page,
    pageSize: query.data?.pageSize ?? pageSize,
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    isError: query.isError,
    error: query.error,
    refresh: query.refetch,
  }
}

/**
 * Uploads a document and runs extraction synchronously.
 *
 * Extraction happens inside the request, so isPending stays true for the whole
 * extraction — including rasterising and OCR'ing a scanned PDF. The caller must
 * show that state and keep the control disabled rather than assuming a fast
 * response.
 *
 * On success the new draft is surfaced and the review queue is refreshed, so
 * the draft survives navigating away from this screen.
 */
export function useExtractAgentDocument() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (file) => extractAgentDocument(file),
    onSuccess: (data) => {
      if (data?.extractionId) {
        queryClient.invalidateQueries({
          queryKey: ['agent', 'document-extraction', data.extractionId],
        })
      }
      queryClient.invalidateQueries({ queryKey: ['agent', 'document-extractions'] })
    },
  })
}

/**
 * Confirms an extraction draft, creating the operational Document.
 *
 * On success the draft leaves the queue and the new document appears in every
 * list that can now show it, so those are invalidated. The Document's expiry
 * date only becomes operational for the existing expiry monitoring once the
 * reviewer has supplied it here.
 *
 * Invalidation is targeted, not a blanket sweep:
 *   - the agent review queue and the draft itself;
 *   - ['agent','documents'] — the agent document list, which by partial
 *     matching also covers ['agent','documents','expiring',...] so a newly
 *     confirmed expiry date refreshes the expiring registry too;
 *   - the new document's detail, and the owner-scoped list when the document
 *     is employee-owned.
 *
 * The DocumentStatus enum on the list rows and the ExpiringSoon / Active /
 * Overdue vocabulary of the expiring registry are separate backend
 * vocabularies: they are refetched independently and never reconciled here.
 */
export function useConfirmAgentDocumentExtraction() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ extractionId, payload }) =>
      confirmAgentDocumentExtraction(extractionId, payload),
    onSuccess: (data, variables) => {
      queryClient.invalidateQueries({
        queryKey: ['agent', 'document-extraction', variables.extractionId],
      })
      queryClient.invalidateQueries({ queryKey: ['agent', 'document-extractions'] })
      queryClient.invalidateQueries({ queryKey: ['agent', 'documents'] })

      if (data?.documentId) {
        queryClient.invalidateQueries({
          queryKey: ['agent', 'document', data.documentId],
        })
      }

      if (variables.payload?.employeeId) {
        queryClient.invalidateQueries({
          queryKey: ['agent', 'employee', variables.payload.employeeId, 'documents'],
        })
      }
    },
  })
}
