import { useQuery } from '@tanstack/react-query'
import {
  getAdminClientContracts,
  getAdminActiveContract,
  getAdminContractById,
} from '../../api/admin/contracts'

/**
 * Service contracts for one client company.
 *
 * APPROVED DECISION Q8: contracts are presented inside the Admin Client detail
 * drawer, not on their own page, so these hooks are always driven by a
 * clientCompanyId rather than a global list.
 *
 * Backing query GET /api/v1/clients/{clientCompanyId}/contracts
 * NOTE: this response is { items } only — no totalCount, so the drawer cannot
 * show a contract count without loading every page.
 */
export function useAdminClientContracts(clientCompanyId, params = {}) {
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20

  const query = useQuery({
    queryKey: ['admin', 'contracts', clientCompanyId, { page, pageSize }],
    queryFn: () => getAdminClientContracts(clientCompanyId, { page, pageSize }),
    enabled: Boolean(clientCompanyId),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    items: query.data?.items ?? [],
    hasTotalCount: query.data?.totalCount != null,
    totalCount: query.data?.totalCount ?? null,
    page,
    pageSize,
    loading: query.isLoading,
    isLoading: query.isLoading,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}

/**
 * The company's current active contract.
 * Backing query GET /api/v1/clients/{clientCompanyId}/contracts/active
 *
 * CAVEAT: the backend filters on Status == Active only and does not compare
 * dates, so a contract whose EndDate has passed still comes back as active.
 * Surface the EndDate next to the result rather than implying it is current.
 * Resolves to null (not an error) when the company has no active contract.
 */
export function useAdminActiveContract(clientCompanyId) {
  const query = useQuery({
    queryKey: ['admin', 'contracts', clientCompanyId, 'active'],
    queryFn: () => getAdminActiveContract(clientCompanyId),
    enabled: Boolean(clientCompanyId),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data ?? null,
    loading: query.isLoading,
    isLoading: query.isLoading,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}

/**
 * Single contract.
 * Backing query GET /api/v1/contracts/{id}
 */
export function useAdminContract(contractId) {
  const query = useQuery({
    queryKey: ['admin', 'contract', contractId],
    queryFn: () => getAdminContractById(contractId),
    enabled: Boolean(contractId),
    staleTime: 30_000,
    retry: 1,
  })

  return {
    data: query.data,
    loading: query.isLoading,
    isLoading: query.isLoading,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}
