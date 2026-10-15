import { useQuery } from '@tanstack/react-query'
import { getAdminPaymentOrders, getAdminPaymentOrderById } from '../../api/admin/payments'

/**
 * Paginated Admin payment-order list.
 * Backing query GET /api/v1/payment-orders
 * Params: { clientCompanyId?, status?, page = 1, pageSize = 20 }
 *
 * ------------------------------------------------------------------
 * NO `status` IS EVER SENT, and the reason is a backend defect.
 * ------------------------------------------------------------------
 * GetPaymentOrdersQuery DOES bind `PaymentStatus? Status`, so an earlier version of
 * this file concluded the value "must be sent as a NUMBER (1..6) because the DTO
 * declares it `int`". That was wrong, and the consequence was a control that broke
 * the page.
 *
 * The query validator accepts the parameter, and the controller forwards it, but
 * PaymentOrderReadRepository.GetListAsync takes it as `string? status` and builds
 *
 *     conditions.Add("po.\"Status\" = @Status");
 *     parameters.Add("Status", status);
 *
 * while the column is an integer. PostgreSQL therefore rejects the comparison with
 * `operator does not exist: integer = text` and the request returns 500. Whether
 * the value is sent as the enum name or as the ordinal, the parameter reaches Dapper
 * as text, so no encoding choice on this side can make the comparison valid. The
 * filter is simply unusable.
 *
 * So `status` is accepted here for key symmetry with the other Admin list hooks and
 * is then NOT forwarded to the API, and the Payments tab exposes no status filter at
 * all. It is removed from the request rather than defaulted, because sending an empty
 * value would still be a status the repository has to reason about.
 *
 * Rows already carry clientCompanyName, so no client map lookup is needed.
 *
 * APPROVED DECISION Q5: create and cancel are later phases and must be
 * confirmed through the shared ConfirmDialog, so no mutation hook exists yet.
 */
export function useAdminPaymentOrders(params = {}) {
  const clientCompanyId = params?.clientCompanyId
  // Read but deliberately NOT forwarded: see the note above on the unusable
  // repository predicate. It stays in the key so the shape of this hook matches the
  // other Admin list hooks and so removing the control later is a one-line change.
  const status = params?.status
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20

  const query = useQuery({
    queryKey: ['admin', 'payments', { clientCompanyId, status, page, pageSize }],
    queryFn: () => getAdminPaymentOrders({ clientCompanyId, page, pageSize }),
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

/** GET /api/v1/payment-orders/{id} */
export function useAdminPaymentOrder(id) {
  const query = useQuery({
    queryKey: ['admin', 'payment', id],
    queryFn: () => getAdminPaymentOrderById(id),
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
