import { useQuery } from '@tanstack/react-query'
import { getAgentDocuments } from '../../api/agent/documents'

/**
 * Loads the Agent-scoped, paginated and filterable document list.
 * Backing query GET /api/v1/agent/documents
 * Params: { type?, status?, expiresBefore?, page = 1, pageSize = 20 }
 * Returns { data, items, totalCount, page, pageSize, loading, isLoading,
 *   error, isError, refresh }
 *   item: AgentDocumentListItemDto { id, clientEntityId?, employeeId?, type,
 *     documentNumber, issueDate?, expiryDate?, fileUrl?, fileName?,
 *     contentType?, fileSize?, isActive, isDeleted, createdAt, updatedAt,
 *     status }
 */
export function useAgentDocuments(params = {}) {
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20
  const type = params?.type
  const status = params?.status
  const expiresBefore = params?.expiresBefore

  const query = useQuery({
    queryKey: ['agent', 'documents', { page, pageSize, type, status, expiresBefore }],
    queryFn: () => getAgentDocuments({ page, pageSize, type, status, expiresBefore }),
    staleTime: 30_000,
    retry: 1,
  })

  const items = query.data?.items ?? []

  return {
    data: query.data,
    items,
    totalCount: query.data?.totalCount ?? 0,
    page: query.data?.page ?? page,
    pageSize: query.data?.pageSize ?? pageSize,
    loading: query.isLoading,
    isLoading: query.isLoading,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}
