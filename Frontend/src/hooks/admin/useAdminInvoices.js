import { useQuery } from '@tanstack/react-query'
import {
  getAdminRetainerInvoices,
  getAdminRetainerInvoiceById,
  getAdminServiceFeeInvoices,
  getAdminServiceFeeInvoiceById,
  getAdminGovFeeDisbursements,
  getAdminGovFeeDisbursementById,
} from '../../api/admin/invoices'

/**
 * Admin invoice hooks.
 *
 * APPROVED DECISION Q9: Retainer, Service Fee and Government Fee are presented
 * as tabs of one Invoices page. These hooks stay separate because the three
 * resources have distinct routes, filters and status enums.
 *
 * `status` is sent as the enum NAME ("Pending" | "Paid" | "Void"), which
 * query-string binding resolves case-insensitively.
 *
 * Invoice rows carry clientCompanyId but no company name, so tables resolve it
 * with useAdminEntityMaps.
 *
 * NO MUTATION HOOKS: mark-paid, void and disbursement status changes are later
 * phases and must run behind the shared ConfirmDialog. The two PDF GETs are
 * read-only and are wrapped in the API layer, but they are deliberately NOT query
 * hooks: a PDF is a file, not cacheable state, so it is fetched on demand by the
 * download button rather than prefetched into the query cache.
 */

/** GET /api/v1/retainer-invoices — { clientCompanyId?, serviceContractId?, status?, page, pageSize } */
export function useAdminRetainerInvoices(params = {}) {
  const clientCompanyId = params?.clientCompanyId
  const serviceContractId = params?.serviceContractId
  const status = params?.status
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20

  const query = useQuery({
    queryKey: ['admin', 'invoices', 'retainer', { clientCompanyId, serviceContractId, status, page, pageSize }],
    queryFn: () => getAdminRetainerInvoices({ clientCompanyId, serviceContractId, status, page, pageSize }),
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
    // isFetching is separate from isLoading on purpose: it is true again on every
    // background refetch (a filter change, a tab switch, a window focus), which is
    // what the table's "stale rows, still updating" state and the filter bar's
    // refresh indicator need in order to tell themselves apart from the first load.
    isFetching: query.isFetching,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}

/** GET /api/v1/retainer-invoices/{id} */
export function useAdminRetainerInvoice(id) {
  const query = useQuery({
    queryKey: ['admin', 'invoice', 'retainer', id],
    queryFn: () => getAdminRetainerInvoiceById(id),
    enabled: Boolean(id),
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

/**
 * GET /api/v1/service-fee-invoices — { clientCompanyId?, renewalTaskId?, status?, page, pageSize }
 *
 * This is a LIST hook and intentionally exposes no create action. Creation is not
 * missing from the module: `useCreateAdminServiceFeeInvoice` in
 * useAdminBillingMutations drives AdminServiceFeeInvoiceFormDialog, which
 * AdminInvoicesPage renders.
 *
 * An earlier version of this comment stated that no create action is exposed
 * because "the task-completion path that would populate ServiceFeeAmount is
 * currently unreachable". Both halves were inaccurate: the amount IS assignable via
 * PATCH /api/v1/admin/tasks/{taskId}/service-fee, and the create dialog is wired.
 *
 * The real open question is a different one (F-33): completion itself creates the
 * service-fee invoice inside the status transaction, so a task that reached
 * `Updated` through the Agent flow already has one and the dialog gets the
 * server's 409. That product decision is NOT resolved here.
 */
export function useAdminServiceFeeInvoices(params = {}) {
  const clientCompanyId = params?.clientCompanyId
  const renewalTaskId = params?.renewalTaskId
  const status = params?.status
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20

  const query = useQuery({
    queryKey: ['admin', 'invoices', 'service-fee', { clientCompanyId, renewalTaskId, status, page, pageSize }],
    queryFn: () => getAdminServiceFeeInvoices({ clientCompanyId, renewalTaskId, status, page, pageSize }),
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
    isFetching: query.isFetching,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}

/** GET /api/v1/service-fee-invoices/{id} */
export function useAdminServiceFeeInvoice(id) {
  const query = useQuery({
    queryKey: ['admin', 'invoice', 'service-fee', id],
    queryFn: () => getAdminServiceFeeInvoiceById(id),
    enabled: Boolean(id),
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

/** GET /api/v1/gov-fee-disbursements — { clientCompanyId?, status?, page, pageSize } */
export function useAdminGovFeeDisbursements(params = {}) {
  const clientCompanyId = params?.clientCompanyId
  const status = params?.status
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20

  const query = useQuery({
    queryKey: ['admin', 'invoices', 'gov-fee', { clientCompanyId, status, page, pageSize }],
    queryFn: () => getAdminGovFeeDisbursements({ clientCompanyId, status, page, pageSize }),
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
    isFetching: query.isFetching,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}

/** GET /api/v1/gov-fee-disbursements/{id} */
export function useAdminGovFeeDisbursement(id) {
  const query = useQuery({
    queryKey: ['admin', 'invoice', 'gov-fee', id],
    queryFn: () => getAdminGovFeeDisbursementById(id),
    enabled: Boolean(id),
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
