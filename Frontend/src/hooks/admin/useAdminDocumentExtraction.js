import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  confirmAdminDocumentExtraction,
  extractAdminDocument,
  getAdminDocumentExtraction,
  getAdminPendingDocumentExtractions,
} from '../../api/admin/documents'

/**
 * D1 — Admin document registration: upload, review queue, confirm.
 *
 * QUERY KEYS (matching the verified existing Admin keys):
 *   ['admin','document-extraction', id]      one draft
 *   ['admin','document-extractions', {...}]  the paged review queue
 *
 * These are new prefixes rather than a reuse of ['admin','document',id]: a
 * draft is not a document and must not invalidate the confirmed-document
 * caches, nor be invalidated by them.
 */

/**
 * Loads one extraction draft's review payload.
 * Backing query GET /api/v1/admin/documents/extractions/{id}
 * Returns { data, isLoading, isError, error, refresh }.
 */
export function useAdminDocumentExtraction(extractionId) {
  const query = useQuery({
    queryKey: ['admin', 'document-extraction', extractionId],
    queryFn: () => getAdminDocumentExtraction(extractionId),
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
 * Loads the paged queue of extraction drafts awaiting review.
 * Backing query GET /api/v1/admin/documents/extractions
 * Params: { page = 1, pageSize = 20, status? }
 * Returns { data, items, totalCount, page, pageSize, isLoading, isError,
 *   error, isFetching, refresh }.
 */
export function useAdminPendingDocumentExtractions(params = {}) {
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20
  const status = params?.status

  const query = useQuery({
    queryKey: ['admin', 'document-extractions', { page, pageSize, status }],
    queryFn: () => getAdminPendingDocumentExtractions({ page, pageSize, status }),
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
 */
export function useExtractAdminDocument() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (file) => extractAdminDocument(file),
    onSuccess: (data) => {
      if (data?.extractionId) {
        queryClient.invalidateQueries({
          queryKey: ['admin', 'document-extraction', data.extractionId],
        })
      }
      queryClient.invalidateQueries({ queryKey: ['admin', 'document-extractions'] })
    },
  })
}

/**
 * Confirms an extraction draft, creating the operational Document.
 *
 * On success the draft leaves the queue and the new document becomes visible in
 * the Admin views that can already reach it, so exactly those are invalidated:
 *
 *   - the review queue and the draft itself;
 *   - ['admin','documents','expiring'] — the expiry registry, since a confirmed
 *     expiry date is what feeds it;
 *   - the owner-scoped lists, only when that owner was chosen;
 *   - the new document's own detail.
 *
 * The DocumentStatus enum on the document rows and the ExpiringSoon / Active /
 * Overdue vocabulary of the expiry registry are separate backend vocabularies:
 * they are refetched independently and never reconciled here.
 */
export function useConfirmAdminDocumentExtraction() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ extractionId, payload }) =>
      confirmAdminDocumentExtraction(extractionId, payload),
    onSuccess: (data, variables) => {
      queryClient.invalidateQueries({
        queryKey: ['admin', 'document-extraction', variables.extractionId],
      })
      queryClient.invalidateQueries({ queryKey: ['admin', 'document-extractions'] })

      queryClient.invalidateQueries({
        queryKey: ['admin', 'documents', 'expiring'],
      })

      if (data?.documentId) {
        queryClient.invalidateQueries({
          queryKey: ['admin', 'document', data.documentId],
        })
      }

      if (variables.payload?.employeeId) {
        queryClient.invalidateQueries({
          queryKey: ['admin', 'employee-documents', variables.payload.employeeId],
        })
      }

      if (variables.payload?.clientEntityId) {
        queryClient.invalidateQueries({
          queryKey: ['admin', 'entity-documents', variables.payload.clientEntityId],
        })
      }
    },
  })
}
